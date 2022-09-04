﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using VampireCommandFramework.Common;
using VampireCommandFramework.Registry;

namespace VampireCommandFramework;

public static class CommandRegistry
{
	private const string DEFAULT_PREFIX = ".";
	private static CommandCache _cache = new();
	private static Dictionary<Type, (object instance, MethodInfo tryParse)> _converters = new();

	internal static void Reset()
	{
		// testability and a bunch of static crap, I know...
		Middlewares.Clear();
		Middlewares.AddRange(DEFAULT_MIDDLEWARES);
		_converters.Clear();
		_cache = new();
	}

	// todo: document this default behavior, it's just not something to ship without but you can Middlewares.Claer();
	private static List<CommandMiddleware> DEFAULT_MIDDLEWARES = new() { new VCF.Core.Basics.BasicAdminCheck() };
	public static List<CommandMiddleware> Middlewares { get; } = new() { new VCF.Core.Basics.BasicAdminCheck() };

	public static CommandResult Handle(ICommandContext ctx, string input)
	{
		static bool HandleCanExecute(ICommandContext ctx, CommandMetadata command)
		{
			Log.Debug($"Executing {Middlewares.Count} CanHandle Middlwares:");
			foreach (var middleware in Middlewares)
			{

				Log.Debug($"\t{middleware.GetType().Name}");
				if (!middleware.CanExecute(ctx, command.Attribute, command.Method))
				{
					return false;
				}
			}
			return true;
		}

		// todo: rethink, maybe you only want 1 door here, people will confuse these and it's probably possible to collapse
		static void HandleBeforeExecute(ICommandContext ctx, CommandMetadata command)
		{
			Middlewares.ForEach(m => m.BeforeExecute(ctx, command.Attribute, command.Method));
		}

		static void HandleAfterExecute(ICommandContext ctx, CommandMetadata command)
		{
			Middlewares.ForEach(m => m.AfterExecute(ctx, command.Attribute, command.Method));
		}


		var (command, args) = _cache.GetCommand(input);

		if (command == null) return CommandResult.Unmatched; // NOT FOUND

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
			return CommandResult.InternalError;
		}

		var argCount = args.Length;
		var paramsCount = command.Parameters.Length;
		var commandArgs = new object[paramsCount + 1];
		commandArgs[0] = ctx;

		// Handle default values
		if (argCount != paramsCount)
		{
			var canDefault = command.Parameters.Skip(argCount).All(p => p.HasDefaultValue);
			if (!canDefault)
			{
				// todo: error you bad at defaulting values, how you explain to someone?
				return CommandResult.UsageError;
			}
			for (var i = argCount; i < paramsCount; i++)
			{
				commandArgs[i + 1] = command.Parameters[i].DefaultValue;
			}
		}

		// Handle Converting Parameters
		for (var i = 0; i < argCount; i++)
		{
			var param = command.Parameters[i];
			var arg = args[i];

			if (_converters.TryGetValue(param.ParameterType, out var customConverter))
			{
				var (converter, convertMethod) = customConverter;

				object result;
				var tryParseArgs = new object[] { ctx, arg };
				try
				{
					result = convertMethod.Invoke(converter, tryParseArgs);
					commandArgs[i + 1] = result;
				}
				catch (TargetInvocationException tie) when (tie.InnerException is CommandException e)
				{
					// todo: error matched type but failed to convert arg to type
					ctx.Reply($"<color=red>[error]</color> Failed converted parameter: {e.Message}");
					return CommandResult.UsageError;
				}
				catch (Exception e)
				{
					// todo: failed custom converter unhandled
					Log.Warning($"Hit unexpected exception {e}");
					return CommandResult.InternalError;
				}
			}
			else
			{
				// default convertet
				var builtinConverter = TypeDescriptor.GetConverter(param.ParameterType);
				try
				{
					commandArgs[i + 1] = builtinConverter.ConvertFromInvariantString(arg);
				}
				catch (Exception e)
				{
					ctx.Reply($"<color=red>[error]</color> Failed converted parameter: {e.Message}");
					return CommandResult.UsageError;
				}
			}
		}

		object instance = null;
		// construct command's type with context if declared only in a non-static class and on a non-static method
		if (!command.Method.IsStatic && !(command.Method.DeclaringType.IsAbstract && command.Method.DeclaringType.IsSealed))
		{
			instance = command.Constructor == null ? Activator.CreateInstance(command.Method.DeclaringType) : command.Constructor.Invoke(new[] { ctx });
		}

		// Handle Middlewares
		if (!HandleCanExecute(ctx, command))
		{
			ctx.Reply($"<color=red>[denied]</color> {command.Attribute.Id}");
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
			ctx.Reply($"<color=red>[error]</color> {e.Message}");
			return CommandResult.CommandError;
		}
		catch (Exception e)
		{
			Log.Warning($"Hit unexpected exception executing command {command.Attribute.Id}\n: {e}");
			return CommandResult.InternalError;
		}

		HandleAfterExecute(ctx, command);

		return CommandResult.Success;
	}

	public static void RegisterConverter(Type converter)
	{
		// check base type
		if (converter.BaseType.Name != typeof(CommandArgumentConverter<>).Name)
		{
			return;
		}

		object converterInstance = Activator.CreateInstance(converter);
		MethodInfo methodInfo = converter.GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
		if (methodInfo == null)
		{
			// can't bud
			Log.Error("Can't find TryParse that matches");
			return;
		}

		var convertFrom = converter.BaseType.GenericTypeArguments?.SingleOrDefault();
		if (convertFrom == null)
		{
			Log.Error("Can't determine generic base type to convert from. ");
			return;
		}

		_converters.Add(convertFrom, (converterInstance, methodInfo));
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
			.Where(c => typeof(ICommandContext).IsAssignableFrom(c.GetParameters().SingleOrDefault()?.ParameterType))
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

		var command = new CommandMetadata(commandAttr, method, customConstructor, parameters, first.ParameterType, constructorType);

		// todo include prefix and group in here, this shoudl be a string match
		// todo handle collisons here

		// BAD CODE INC.. permute and cache keys -> command
		var groupNames = groupAttr == null ? new[] { "" } : groupAttr.ShortHand == null ? new[] { $"{groupAttr.Name} " } : new[] { $"{groupAttr.Name} ", $"{groupAttr.ShortHand} ", };
		var names = commandAttr.ShortHand == null ? new[] { commandAttr.Name } : new[] { commandAttr.Name, commandAttr.ShortHand };
		var prefix = DEFAULT_PREFIX; // TODO: get from attribute/config
		foreach (var group in groupNames)
		{
			foreach (var name in names)
			{
				var key = $"{prefix}{group}{name}";
				_cache.AddCommand(key, parameters, command);
			}
		}
	}

	public static void UnregisterAssembly(Assembly assembly)
	{
		foreach (var type in assembly.DefinedTypes)
		{
			_cache.RemoveCommandsFromType(type);
		}
	}
}
