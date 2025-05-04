using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using VampireCommandFramework.Basics;
using VampireCommandFramework.Common;
using VampireCommandFramework.Registry;

using static VampireCommandFramework.Format;

namespace VampireCommandFramework;

public static class CommandRegistry
{
	internal const string DEFAULT_PREFIX = ".";
	internal static CommandCache _cache = new();
	/// <summary>
	/// From converting type to (object instance, MethodInfo tryParse, Type contextType)
	/// </summary>
	internal static Dictionary<Type, (object instance, MethodInfo tryParse, Type contextType)> _converters = [];

	internal static void Reset()
	{
		// testability and a bunch of static crap, I know...
		Middlewares.Clear();
		Middlewares.AddRange(DEFAULT_MIDDLEWARES);
		AssemblyCommandMap.Clear();
		_converters.Clear();
		_cache = new();
	}

	// todo: document this default behavior, it's just not something to ship without but you can Middlewares.Claer();
	private static List<CommandMiddleware> DEFAULT_MIDDLEWARES = new() { new VCF.Core.Basics.BasicAdminCheck() };
	public static List<CommandMiddleware> Middlewares { get; } = new() { new VCF.Core.Basics.BasicAdminCheck() };

	// Store pending commands for selection
	private static Dictionary<string, List<(CommandMetadata Command, object[] Args, string Error)>> _pendingCommands = [];

	internal static bool CanCommandExecute(ICommandContext ctx, CommandMetadata command)
	{
		// Log.Debug($"Executing {Middlewares.Count} CanHandle Middlwares:");
		foreach (var middleware in Middlewares)
		{
			// Log.Debug($"\t{middleware.GetType().Name}");
			try
			{
				if (!middleware.CanExecute(ctx, command.Attribute, command.Method))
				{
					return false;
				}
			}
			catch (Exception e)
			{
				Log.Error($"Error executing {middleware.GetType().Name} {e}");
				return false;
			}
		}
		return true;
	}

	private static void HandleBeforeExecute(ICommandContext ctx, CommandMetadata command)
	{
		Middlewares.ForEach(m => m.BeforeExecute(ctx, command.Attribute, command.Method));
	}

	private static void HandleAfterExecute(ICommandContext ctx, CommandMetadata command)
	{
		Middlewares.ForEach(m => m.AfterExecute(ctx, command.Attribute, command.Method));
	}

	public static CommandResult Handle(ICommandContext ctx, string input)
	{
		// Check if this is a command selection (e.g., .1, .2, etc.)
		if (input.StartsWith(DEFAULT_PREFIX) && input.Length > 1)
		{
			string numberPart = input.Substring(1);
			if (int.TryParse(numberPart, out int selectedIndex) && selectedIndex > 0)
			{
				return HandleCommandSelection(ctx, selectedIndex);
			}
		}

		// Ensure the command starts with the prefix
		if (!input.StartsWith(DEFAULT_PREFIX))
		{
			return CommandResult.Unmatched; // Not a command
		}

		// Remove the prefix for processing
		string afterPrefix = input.Substring(DEFAULT_PREFIX.Length);

		// Check if this could be an assembly-specific command
		string assemblyName = null;
		string commandInput = input; // Default to using the entire input

		int spaceIndex = afterPrefix.IndexOf(' ');
		if (spaceIndex > 0)
		{
			string potentialAssemblyName = afterPrefix.Substring(0, spaceIndex);

			// Check if this could be a valid assembly name
			bool isValidAssembly = AssemblyCommandMap.Keys.Any(a =>
				a.GetName().Name.Equals(potentialAssemblyName, StringComparison.OrdinalIgnoreCase));

			if (isValidAssembly)
			{
				assemblyName = potentialAssemblyName;
				commandInput = "." + afterPrefix.Substring(spaceIndex + 1);
			}
		}

		// Get command(s) based on input
		CacheResult matchedCommand;
		if (assemblyName != null)
		{
			matchedCommand = _cache.GetCommandFromAssembly(commandInput, assemblyName);
		}
		else
		{
			matchedCommand = _cache.GetCommand(input);
		}

		var (commands, args) = (matchedCommand.Commands, matchedCommand.Args);

		if (!matchedCommand.IsMatched)
		{
			if (!matchedCommand.HasPartial) return CommandResult.Unmatched; // NOT FOUND

			foreach (var possible in matchedCommand.PartialMatches)
			{
				ctx.SysReply(HelpCommands.GetShortHelp(possible));
			}

			return CommandResult.UsageError;
		}

		// If there's only one command, handle it directly
		if (commands.Count() == 1)
		{
			return ExecuteCommand(ctx, commands.First(), args);
		}

		// Multiple commands match, try to convert parameters for each
		var successfulCommands = new List<(CommandMetadata Command, object[] Args, string Error)>();
		var failedCommands = new List<(CommandMetadata Command, string Error)>();

		foreach (var command in commands)
		{
			if (!CanCommandExecute(ctx, command)) continue;

			var (success, commandArgs, error) = TryConvertParameters(ctx, command, args);
			if (success)
			{
				successfulCommands.Add((command, commandArgs, null));
			}
			else
			{
				failedCommands.Add((command, error));
			}
		}

		// Case 1: No command succeeded
		if (successfulCommands.Count == 0)
		{
			ctx.Reply($"{"[error]".Color(Color.Red)} Failed to execute command due to parameter conversion errors:");
			foreach (var (command, error) in failedCommands)
			{
				string assemblyInfo = command.Assembly.GetName().Name;
				ctx.Reply($"  - {command.Attribute.Id} ({assemblyInfo}): {error}");
			}
			return CommandResult.UsageError;
		}

		// Case 2: Only one command succeeded
		if (successfulCommands.Count == 1)
		{
			var (command, commandArgs, _) = successfulCommands[0];
			return ExecuteCommandWithArgs(ctx, command, commandArgs);
		}

		// Case 3: Multiple commands succeeded - store and ask user to select
		_pendingCommands[ctx.Name] = successfulCommands;

		var sb = new StringBuilder();
		sb.AppendLine($"Multiple commands match this input. Select one by typing {B(".<#>").Color(Color.Command)}:");
		for (int i = 0; i < successfulCommands.Count; i++)
		{
			var (command, _, _) = successfulCommands[i];
			var cmdAssembly = command.Assembly.GetName().Name;
			var description = command.Attribute.Description;
			sb.AppendLine($" {("."+ (i + 1).ToString()).Color(Color.Command)} - {cmdAssembly.Bold().Color(Color.Primary)} - {B(command.Attribute.Name)} ({command.Attribute.Id}) {command.Attribute.Description}");
			sb.AppendLine("   " + HelpCommands.GetShortHelp(command));
		}
		ctx.SysPaginatedReply(sb);

		return CommandResult.Success;
	}

	// Add these helper methods:

	private static CommandResult HandleCommandSelection(ICommandContext ctx, int selectedIndex)
	{
		if (!_pendingCommands.TryGetValue(ctx.Name, out var pendingCommands) || pendingCommands.Count == 0)
		{
			ctx.Reply($"{"[error]".Color(Color.Red)} No command selection is pending.");
			return CommandResult.CommandError;
		}

		if (selectedIndex < 1 || selectedIndex > pendingCommands.Count)
		{
			ctx.Reply($"{"[error]".Color(Color.Red)} Invalid selection. Please select a number between 1 and {pendingCommands.Count}.");
			return CommandResult.UsageError;
		}

		var (command, args, _) = pendingCommands[selectedIndex - 1];

		// Clear pending commands after selection
		var result = ExecuteCommandWithArgs(ctx, command, args);
		pendingCommands.Clear();
		return result;
	}

	private static (bool Success, object[] Args, string Error) TryConvertParameters(ICommandContext ctx, CommandMetadata command, string[] args)
	{
		var argCount = args?.Length ?? 0;
		var paramsCount = command.Parameters.Length;
		var commandArgs = new object[paramsCount + 1];
		commandArgs[0] = ctx;

		// Special case for commands with no parameters
		if (paramsCount == 0 && argCount == 0)
		{
			return (true, commandArgs, null);
		}

		// Handle parameter count mismatch
		if (argCount > paramsCount)
		{
			return (false, null, $"Too many parameters: expected {paramsCount}, got {argCount}");
		}
		else if (argCount < paramsCount)
		{
			var canDefault = command.Parameters.Skip(argCount).All(p => p.HasDefaultValue);
			if (!canDefault)
			{
				return (false, null, $"Missing required parameters: expected {paramsCount}, got {argCount}");
			}
			for (var i = argCount; i < paramsCount; i++)
			{
				commandArgs[i + 1] = command.Parameters[i].DefaultValue;
			}
		}

		// If we have arguments to convert, process them
		if (argCount > 0)
		{
			for (var i = 0; i < argCount; i++)
			{
				var param = command.Parameters[i];
				var arg = args[i];
				bool conversionSuccess = false;
				string conversionError = null;

				try
				{
					// Custom Converter
					if (_converters.TryGetValue(param.ParameterType, out var customConverter))
					{
						var (converter, convertMethod, converterContextType) = customConverter;

						// IMPORTANT CHANGE: Return special error code for unassignable context
						if (!converterContextType.IsAssignableFrom(ctx.GetType()))
						{
							// Signal internal error with a special return format
							return (false, null, $"INTERNAL_ERROR:Converter type {converterContextType.Name} is not assignable from {ctx.GetType().Name}");
						}

						object result;
						var tryParseArgs = new object[] { ctx, arg };
						try
						{
							result = convertMethod.Invoke(converter, tryParseArgs);
							commandArgs[i + 1] = result;
							conversionSuccess = true;
						}
						catch (TargetInvocationException tie)
						{
							if (tie.InnerException is CommandException e)
							{
								conversionError = $"Parameter {i + 1} ({param.Name}): {e.Message}";
							}
							else
							{
								conversionError = $"Parameter {i + 1} ({param.Name}): Unexpected error converting parameter";
							}
						}
						catch (Exception)
						{
							conversionError = $"Parameter {i + 1} ({param.Name}): Unexpected error converting parameter";
						}
					}
					else
					{
						var defaultConverter = TypeDescriptor.GetConverter(param.ParameterType);
						try
						{
							var val = defaultConverter.ConvertFromInvariantString(arg);

							// Separate, more robust enum validation
							if (param.ParameterType.IsEnum)
							{
								bool isDefined = false;

								// For numeric input, we need to check if the value is defined
								if (int.TryParse(arg, out int enumIntVal))
								{
									isDefined = Enum.IsDefined(param.ParameterType, enumIntVal);

									if (!isDefined)
									{
										return (false, null, $"Parameter {i + 1} ({param.Name}): Invalid enum value '{arg}' for {param.ParameterType.Name}");
									}
								}
							}

							commandArgs[i + 1] = val;
							conversionSuccess = true;
						}
						catch (Exception e)
						{
							conversionError = $"Parameter {i + 1} ({param.Name}): {e.Message}";
						}
					}
				}
				catch (Exception ex)
				{
					conversionError = $"Parameter {i + 1} ({param.Name}): Unexpected error: {ex.Message}";
				}

				if (!conversionSuccess)
				{
					return (false, null, conversionError);
				}
			}
		}

		return (true, commandArgs, null);
	}

	private static CommandResult ExecuteCommand(ICommandContext ctx, CommandMetadata command, string[] args)
	{
		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Id}] but can not assign {command.ContextType.Name} from {ctx?.GetType().Name}");
			return CommandResult.InternalError;
		}

		// Try to convert parameters
		var (success, commandArgs, error) = TryConvertParameters(ctx, command, args);
		if (!success)
		{
			// Check for special internal error flag
			if (error != null && error.StartsWith("INTERNAL_ERROR:"))
			{
				string actualError = error.Substring("INTERNAL_ERROR:".Length);
				Log.Warning(actualError);
				ctx.InternalError();
				return CommandResult.InternalError;
			}

			ctx.Reply($"{"[error]".Color(Color.Red)} {error}");
			return CommandResult.UsageError;
		}

		return ExecuteCommandWithArgs(ctx, command, commandArgs);
	}

	private static CommandResult ExecuteCommandWithArgs(ICommandContext ctx, CommandMetadata command, object[] commandArgs)
	{
		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Id}] but can not assign {command.ContextType.Name} from {ctx?.GetType().Name}");
			return CommandResult.InternalError;
		}

		// Then handle this invocation's context not being valid for the command classes custom constructor
		if (command.Constructor != null && !command.ConstructorType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Id}] but can not assign {command.ConstructorType.Name} from {ctx?.GetType().Name}");
			ctx.InternalError();
			return CommandResult.InternalError;
		}

		object instance = null;
		// construct command's type with context if declared only in a non-static class and on a non-static method
		if (!command.Method.IsStatic && !(command.Method.DeclaringType.IsAbstract && command.Method.DeclaringType.IsSealed))
		{
			try
			{
				instance = command.Constructor == null ? Activator.CreateInstance(command.Method.DeclaringType) : command.Constructor.Invoke(new[] { ctx });
			}
			catch (TargetInvocationException tie)
			{
				if (tie.InnerException is CommandException ce)
				{
					ctx.SysReply(ce.Message);
				}
				else
				{
					ctx.InternalError();
				}

				return CommandResult.InternalError;
			}
		}

		// Handle Middlewares
		if (!CanCommandExecute(ctx, command))
		{
			ctx.Reply($"{"[denied]".Color(Color.Red)} {command.Attribute.Id}");
			return CommandResult.Denied;
		}

		HandleBeforeExecute(ctx, command);

		// Execute Command
		try
		{
			command.Method.Invoke(instance, commandArgs);
		}
		catch (TargetInvocationException tie) when (tie.InnerException is CommandException e)
		{
			ctx.Reply($"{"[error]".Color(Color.Red)} {e.Message}");
			return CommandResult.CommandError;
		}
		catch (Exception e)
		{
			Log.Warning($"Hit unexpected exception executing command {command.Attribute.Id}\n: {e}");
			ctx.InternalError();
			return CommandResult.InternalError;
		}

		HandleAfterExecute(ctx, command);

		return CommandResult.Success;
	}

	public static void UnregisterConverter(Type converter)
	{
		if (!IsGenericConverterContext(converter) && !IsSpecificConverterContext(converter))
		{
			return;
		}

		var args = converter.BaseType.GenericTypeArguments;
		var convertFrom = args.FirstOrDefault();
		if (convertFrom == null)
		{
			Log.Warning($"Could not resolve converter type {converter.Name}");
			return;
		}

		if (_converters.ContainsKey(convertFrom))
		{
			_converters.Remove(convertFrom);
			Log.Info($"Unregistered converter {converter.Name}");
		}
		else
		{
			Log.Warning($"Call to UnregisterConverter for a converter that was not registered. Type: {converter.Name}");
		}
	}

	internal static bool IsGenericConverterContext(Type rootType) => rootType?.BaseType?.Name == typeof(CommandArgumentConverter<>).Name;
	internal static bool IsSpecificConverterContext(Type rootType) => rootType?.BaseType?.Name == typeof(CommandArgumentConverter<,>).Name;

	public static void RegisterConverter(Type converter)
	{
		// check base type
		var isGenericContext = IsGenericConverterContext(converter);
		var isSpecificContext = IsSpecificConverterContext(converter);

		if (!isGenericContext && !isSpecificContext)
		{
			return;
		}

		Log.Debug($"Trying to process {converter} as specifc={isSpecificContext} generic={isGenericContext}");

		object converterInstance = Activator.CreateInstance(converter);
		MethodInfo methodInfo = converter.GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
		if (methodInfo == null)
		{
			// can't bud
			Log.Error("Can't find TryParse that matches");
			return;
		}

		var args = converter.BaseType.GenericTypeArguments;
		var convertFrom = args.FirstOrDefault();
		if (convertFrom == null)
		{
			Log.Error("Can't determine generic base type to convert from. ");
			return;
		}

		Type contextType = typeof(ICommandContext);
		if (isSpecificContext)
		{
			if (args.Length != 2 || !typeof(ICommandContext).IsAssignableFrom(args[1]))
			{
				Log.Error("Can't determine generic base type to convert from.");
				return;
			}

			contextType = args[1];
		}


		_converters.Add(convertFrom, (converterInstance, methodInfo, contextType));
	}

	public static void RegisterAll() => RegisterAll(Assembly.GetCallingAssembly());

	public static void RegisterAll(Assembly assembly)
	{
		var types = assembly.GetTypes();

		// Register Converters first as typically commands will depend on them.
		foreach (var type in types)
		{
			RegisterConverter(type);
		}

		foreach (var type in types)
		{
			RegisterCommandType(type);
		}
	}

	public static void RegisterCommandType(Type type)
	{
		var groupAttr = type.GetCustomAttribute<CommandGroupAttribute>();
		var assembly = type.Assembly;
		if (groupAttr != null)
		{
			// handle groups - IDK later
		}

		var methods = type.GetMethods();

		ConstructorInfo contextConstructor = type.GetConstructors()
			.Where(c => c.GetParameters().Length == 1 && typeof(ICommandContext).IsAssignableFrom(c.GetParameters().SingleOrDefault()?.ParameterType))
			.FirstOrDefault();

		foreach (var method in methods)
		{
			RegisterMethod(assembly, groupAttr, contextConstructor, method);
		}
	}

	private static void RegisterMethod(Assembly assembly, CommandGroupAttribute groupAttr, ConstructorInfo customConstructor, MethodInfo method)
	{
		var commandAttr = method.GetCustomAttribute<CommandAttribute>();
		if (commandAttr == null) return;

		// check for CommandContext as first argument to method
		var paramInfos = method.GetParameters();
		var first = paramInfos.FirstOrDefault();
		if (first == null || first.ParameterType is ICommandContext)
		{
			Log.Error($"Method {method.Name} has no CommandContext as first argument");
			return;
		}

		var parameters = paramInfos.Skip(1).ToArray();

		var canConvert = parameters.All(param =>
		{
			if (_converters.ContainsKey(param.ParameterType))
			{
				Log.Debug($"Method {method.Name} has a parameter of type {param.ParameterType.Name} which is registered as a converter");
				return true;
			}

			var converter = TypeDescriptor.GetConverter(param.ParameterType);
			if (converter == null ||
				!converter.CanConvertFrom(typeof(string)))
			{
				Log.Warning($"Parameter {param.Name} could not be converted, so {method.Name} will be ignored.");
				return false;
			}

			return true;
		});

		if (!canConvert) return;

		var constructorType = customConstructor?.GetParameters().Single().ParameterType;

		var command = new CommandMetadata(commandAttr, assembly, method, customConstructor, parameters, first.ParameterType, constructorType, groupAttr);

		// todo include prefix and group in here, this shoudl be a string match
		// todo handle collisons here

		// BAD CODE INC.. permute and cache keys -> command
		var groupNames = groupAttr == null ? new[] { "" } : groupAttr.ShortHand == null ? new[] { $"{groupAttr.Name} " } : new[] { $"{groupAttr.Name} ", $"{groupAttr.ShortHand} ", };
		var names = commandAttr.ShortHand == null ? new[] { commandAttr.Name } : new[] { commandAttr.Name, commandAttr.ShortHand };
		var prefix = DEFAULT_PREFIX; // TODO: get from attribute/config
		List<string> keys = new();
		foreach (var group in groupNames)
		{
			foreach (var name in names)
			{
				var key = $"{prefix}{group}{name}";
				_cache.AddCommand(key, parameters, command);
				keys.Add(key);
			}
		}

		AssemblyCommandMap.TryGetValue(assembly, out var commandKeyCache);
		commandKeyCache ??= new();
		commandKeyCache[command] = keys;
		AssemblyCommandMap[assembly] = commandKeyCache;
	}

	internal static Dictionary<Assembly, Dictionary<CommandMetadata, List<string>>> AssemblyCommandMap { get; } = new();

	public static void UnregisterAssembly() => UnregisterAssembly(Assembly.GetCallingAssembly());

	public static void UnregisterAssembly(Assembly assembly)
	{
		foreach (var type in assembly.DefinedTypes)
		{
			_cache.RemoveCommandsFromType(type);
			UnregisterConverter(type);
			// TODO: There's a lot of nasty cases involving cross mod converters that need testing
			// as of right now the guidance should be to avoid depending on converters from a different mod
			// especially if you're hot reloading either.
		}

		AssemblyCommandMap.Remove(assembly);
	}
}
