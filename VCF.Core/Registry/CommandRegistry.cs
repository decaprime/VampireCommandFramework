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
	internal static Dictionary<Type, (object instance, MethodInfo tryParse, Type contextType)> _converters = new();

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
	private static Dictionary<string, (string input, List<(CommandMetadata Command, object[] Args, string Error)> commands)> _pendingCommands = new();

	private static Dictionary<string, List<(string input, CommandMetadata Command, object[] Args)>> _commandHistory = new();
	private const int MAX_COMMAND_HISTORY = 10; // Store up to 10 past commands

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
				Log.Error($"Error executing {middleware.GetType().Name.Color(Color.Gold)} {e}");
				return false;
			}
		}
		return true;
	}

	internal static IEnumerable<string> FindCloseMatches(ICommandContext ctx, string input)
	{
		// Look for the closest matches to the input command
		const int maxResults = 3;

		// Set a more reasonable max distance
		const int maxFixedDistance = 3;  // For short to medium commands
		const double maxRelativeDistance = 0.5; // Max 50% of command length can be different

		// Ensure we have a valid input
		if (string.IsNullOrWhiteSpace(input))
		{
			return Enumerable.Empty<string>();
		}

		// Remove the prefix if it exists to match command names better
		var normalizedInput = input[1..].ToLowerInvariant();

		var maxDistance = Math.Max(maxFixedDistance,
								(int)Math.Ceiling(normalizedInput.Length * maxRelativeDistance));

		// Get all registered commands for comparison
		var allCommands = AssemblyCommandMap.SelectMany(a => a.Value.Keys).ToList();

		// Calculate edit distances and select the closest matches
		var matches = allCommands
			.Where(c => CanCommandExecute(ctx, c))
			.SelectMany(cmd =>
			{
				// Get all possible combinations of group and command names
				var groupNames = cmd.GroupAttribute == null
					? new[] { "" }
					: cmd.GroupAttribute.ShortHand == null
						? new[] { cmd.GroupAttribute.Name + " " }
						: new[] { cmd.GroupAttribute.Name + " ", cmd.GroupAttribute.ShortHand + " " };

				var commandNames = cmd.Attribute.ShortHand == null
					? new[] { cmd.Attribute.Name }
					: new[] { cmd.Attribute.Name, cmd.Attribute.ShortHand };

				return groupNames.SelectMany(group =>
					commandNames.Select(name => new
					{
						FullName = (group + name).ToLowerInvariant(),
						Command = cmd
					}));
			})
			.Select(cmdInfo =>
			{
				// Calculate the Damerau-Levenshtein distance
				var distance = DamerauLevenshteinDistance(normalizedInput, cmdInfo.FullName);

				var maxCmdDistance = Math.Max(maxDistance, (int)Math.Ceiling(normalizedInput.Length * maxRelativeDistance));

				return new { Command = cmdInfo.FullName, Distance = distance, MaxDistance = maxCmdDistance };
			})
			.Where(x => x.Distance <= x.MaxDistance) // Apply adaptive threshold
			.OrderBy(x => x.Distance)
			.DistinctBy(x => x.Command)
			.Take(maxResults)
			.Select(x => "." + x.Command);

		return matches;
	}

	private static float DamerauLevenshteinDistance(string s, string t)
	{
		// Handle edge cases
		if (string.IsNullOrEmpty(s))
			return string.IsNullOrEmpty(t) ? 0 : t.Length;
		if (string.IsNullOrEmpty(t))
			return s.Length;

		// Create distance matrix
		float[,] matrix = new float[s.Length + 1, t.Length + 1];

		// Initialize first row and column
		for (int i = 0; i <= s.Length; i++)
			matrix[i, 0] = i;

		for (int j = 0; j <= t.Length; j++)
			matrix[0, j] = j;

		// Calculate distances
		for (int i = 1; i <= s.Length; i++)
		{
			for (int j = 1; j <= t.Length; j++)
			{
				int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;

				// Standard Levenshtein operations: deletion, insertion, substitution
				matrix[i, j] = Math.Min(Math.Min(
					matrix[i - 1, j] + 1,      // Deletion
					matrix[i, j - 1] + 1),     // Insertion
					matrix[i - 1, j - 1] + 1.5f*cost); // Substitution (slightly higher than just missing/extra letters)

				// Add transposition check (swap)
				if (i > 1 && j > 1 &&
					s[i - 1] == t[j - 2] &&
					s[i - 2] == t[j - 1])
				{
					matrix[i, j] = Math.Min(matrix[i, j],
						matrix[i - 2, j - 2] + cost); // Transposition
				}
			}
		}

		return matrix[s.Length, t.Length];
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

		if (input.Trim().StartsWith(".!"))
		{
			HandleCommandHistory(ctx, input.Trim());
			return CommandResult.Success;
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
			return ExecuteCommand(ctx, commands.First(), args, input);
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
			var sb = new StringBuilder();
			sb.AppendLine($"{"[error]".Color(Color.Red)} Failed to execute command due to parameter conversion errors:");
			foreach (var (command, error) in failedCommands)
			{
				string assemblyInfo = command.Assembly.GetName().Name;
				sb.AppendLine($"  - {command.Attribute.Name} ({assemblyInfo}): {error}");
			}
			ctx.SysPaginatedReply(sb);
			return CommandResult.UsageError;
		}

		// Case 2: Only one command succeeded
		if (successfulCommands.Count == 1)
		{
			var (command, commandArgs, _) = successfulCommands[0];
			AddToCommandHistory(ctx.Name, input, command, commandArgs);
			return ExecuteCommandWithArgs(ctx, command, commandArgs);
		}

		// Case 3: Multiple commands succeeded - store and ask user to select
		_pendingCommands[ctx.Name] = (input, successfulCommands);

		{
			var sb = new StringBuilder();
			sb.AppendLine($"Multiple commands match this input. Select one by typing {B(".<#>").Color(Color.Command)}:");
			for (int i = 0; i < successfulCommands.Count; i++)
			{
				var (command, _, _) = successfulCommands[i];
				var cmdAssembly = command.Assembly.GetName().Name;
				var description = command.Attribute.Description;
				sb.AppendLine($" {("." + (i + 1).ToString()).Color(Color.Command)} - {cmdAssembly.Bold().Color(Color.Primary)} - {B(command.Attribute.Name)} {command.Attribute.Description}");
				sb.AppendLine("   " + HelpCommands.GetShortHelp(command));
			}
			ctx.SysPaginatedReply(sb);
		}

		return CommandResult.Success;
	}

	// Add these helper methods:

	private static CommandResult HandleCommandSelection(ICommandContext ctx, int selectedIndex)
	{
		if (!_pendingCommands.TryGetValue(ctx.Name, out var pendingCommands) || pendingCommands.commands.Count == 0)
		{
			ctx.SysReply($"{"[error]".Color(Color.Red)} No command selection is pending.");
			return CommandResult.CommandError;
		}

		if (selectedIndex < 1 || selectedIndex > pendingCommands.commands.Count)
		{
			ctx.SysReply($"{"[error]".Color(Color.Red)} Invalid selection. Please select a number between {"1".Color(Color.Gold)} and {pendingCommands.commands.Count.ToString().Color(Color.Gold)}.");
			return CommandResult.UsageError;
		}

		var (command, args, _) = pendingCommands.commands[selectedIndex - 1];

		AddToCommandHistory(ctx.Name, pendingCommands.input, command, args);
		var result = ExecuteCommandWithArgs(ctx, command, args);
		_pendingCommands.Remove(ctx.Name);
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
			return (false, null, $"Too many parameters: expected {paramsCount.ToString().Color(Color.Gold)}, got {argCount.ToString().Color(Color.Gold)}");
		}
		else if (argCount < paramsCount)
		{
			var canDefault = command.Parameters.Skip(argCount).All(p => p.HasDefaultValue);
			if (!canDefault)
			{
				return (false, null, $"Missing required parameters: expected {paramsCount.ToString().Color(Color.Gold)}, got {argCount.ToString().Color(Color.Gold)}");
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
							return (false, null, $"INTERNAL_ERROR:Converter type {converterContextType.Name.ToString().Color(Color.Gold)} is not assignable from {ctx.GetType().Name.ToString().Color(Color.Gold)}");
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
								conversionError = $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}): {e.Message}";
							}
							else
							{
								conversionError = $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}): Unexpected error converting parameter";
							}
						}
						catch (Exception)
						{
							conversionError = $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}): Unexpected error converting parameter";
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
										return (false, null, $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}): Invalid enum value '{arg.ToString().Color(Color.Gold)}' for {param.ParameterType.Name.ToString().Color(Color.Gold)}");
									}
								}
							}

							commandArgs[i + 1] = val;
							conversionSuccess = true;
						}
						catch (Exception e)
						{
							conversionError = $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}): {e.Message}";
						}
					}
				}
				catch (Exception ex)
				{
					conversionError = $"Parameter {i + 1} ({param.Name.ToString().Color(Color.Gold)}): Unexpected error: {ex.Message}";
				}

				if (!conversionSuccess)
				{
					return (false, null, conversionError);
				}
			}
		}

		return (true, commandArgs, null);
	}

	private static CommandResult ExecuteCommand(ICommandContext ctx, CommandMetadata command, string[] args, string input)
	{
		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Name.ToString().Color(Color.Gold)}] but can not assign {command.ContextType.Name.ToString().Color(Color.Gold)} from {ctx?.GetType().Name.ToString().Color(Color.Gold)}");
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

			ctx.SysReply($"{"[error]".Color(Color.Red)} {error}");
			return CommandResult.UsageError;
		}

		AddToCommandHistory(ctx.Name, input, command, commandArgs);
		return ExecuteCommandWithArgs(ctx, command, commandArgs);
	}

	private static CommandResult ExecuteCommandWithArgs(ICommandContext ctx, CommandMetadata command, object[] commandArgs)
	{
		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Name.ToString().Color(Color.Gold)}] but can not assign {command.ContextType.Name.ToString().Color(Color.Gold)} from {ctx?.GetType().Name.ToString().Color(Color.Gold)}");
			return CommandResult.InternalError;
		}

		// Then handle this invocation's context not being valid for the command classes custom constructor
		if (command.Constructor != null && !command.ConstructorType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Name.ToString().Color(Color.Gold)}] but can not assign {command.ConstructorType.Name.ToString().Color(Color.Gold)} from {ctx?.GetType().Name.ToString().Color(Color.Gold)}");
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
			ctx.SysReply($"{"[denied]".Color(Color.Red)} {command.Attribute.Name.ToString().Color(Color.Gold)}");
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
			ctx.SysReply($"{"[error]".Color(Color.Red)} {e.Message}");
			return CommandResult.CommandError;
		}
		catch (Exception e)
		{
			Log.Warning($"Hit unexpected exception executing command {command.Attribute.Id.ToString().Color(Color.Gold)}\n: {e}");
			ctx.InternalError();
			return CommandResult.InternalError;
		}

		HandleAfterExecute(ctx, command);

		return CommandResult.Success;
	}

	private static void AddToCommandHistory(string contextName, string input, CommandMetadata command, object[] args)
	{
		// Create the history list for this context if it doesn't exist yet
		if (!_commandHistory.TryGetValue(contextName, out var history))
		{
			history = new List<(string input, CommandMetadata Command, object[] Args)>();
			_commandHistory[contextName] = history;
		}

		// Add the new command to the beginning of the list
		history.Insert(0, (input, command, args));

		// Keep only the most recent MAX_COMMAND_HISTORY commands
		if (history.Count > MAX_COMMAND_HISTORY)
		{
			history.RemoveAt(history.Count - 1);
		}
	}

	private static void HandleCommandHistory(ICommandContext ctx, string input)
	{
		// Remove the ".!" prefix
		string command = input.Substring(2).Trim();

		// Check if the command history exists for this context
		if (!_commandHistory.TryGetValue(ctx.Name, out var history) || history.Count == 0)
		{
			ctx.SysReply($"{"[error]".Color(Color.Red)} No command history available.");
			return;
		}

		// Handle .! list or .! l commands
		if (command == "list" || command == "l")
		{
			var sb = new StringBuilder();
			sb.AppendLine("Command history:");

			for (int i = 0; i < history.Count; i++)
			{
				sb.AppendLine($"{(i + 1).ToString().Color(Color.Gold)}. {history[i].input.Color(Color.Command)}");
			}

			ctx.SysPaginatedReply(sb);
			return;
		}

		// Handle .! # to execute a specific command by number
		if (int.TryParse(command, out int index) && index > 0 && index <= history.Count)
		{
			var selectedCommand = history[index - 1];
			ctx.SysReply($"Executing command {index.ToString().Color(Color.Gold)}: {selectedCommand.input.Color(Color.Command)}");
			ExecuteCommandWithArgs(ctx, selectedCommand.Command, selectedCommand.Args);
			return;
		}

		// If just .! is provided, execute the most recent command
		if (string.IsNullOrWhiteSpace(command))
		{
			var mostRecent = history[0];
			ctx.SysReply($"Repeating most recent command: {mostRecent.input.Color(Color.Command)}");
			ExecuteCommandWithArgs(ctx, mostRecent.Command, mostRecent.Args);
			return;
		}

		// Invalid command
		ctx.SysReply($"{"[error]".Color(Color.Red)} Invalid command history selection. Use {".! list".Color(Color.Command)} to see available commands or {".! #".Color(Color.Command)} to execute a specific command.");
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
			Log.Warning($"Could not resolve converter type {converter.Name.ToString().Color(Color.Gold)}");
			return;
		}

		if (_converters.ContainsKey(convertFrom))
		{
			_converters.Remove(convertFrom);
			Log.Info($"Unregistered converter {converter.Name}");
		}
		else
		{
			Log.Warning($"Call to UnregisterConverter for a converter that was not registered. Type: {converter.Name.ToString().Color(Color.Gold)}");
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
			Log.Error($"Method {method.Name.ToString().Color(Color.Gold)} has no CommandContext as first argument");
			return;
		}

		var parameters = paramInfos.Skip(1).ToArray();

		var canConvert = parameters.All(param =>
		{
			if (_converters.ContainsKey(param.ParameterType))
			{
				Log.Debug($"Method {method.Name.ToString().Color(Color.Gold)} has a parameter of type {param.ParameterType.Name.ToString().Color(Color.Gold)} which is registered as a converter");
				return true;
			}

			var converter = TypeDescriptor.GetConverter(param.ParameterType);
			if (converter == null ||
				!converter.CanConvertFrom(typeof(string)))
			{
				Log.Warning($"Parameter {param.Name.ToString().Color(Color.Gold)} could not be converted, so {method.Name.ToString().Color(Color.Gold)} will be ignored.");
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
