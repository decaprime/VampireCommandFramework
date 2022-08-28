using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace VampireCommandFramework;

public static class CommandRegistry
{
	private const string DEFAULT_PREFIX = ".";
	private static CommandCache _cache = new();
	private static Dictionary<Type, (object instance, MethodInfo tryParse)> _converters = new();

	// also todo: maybe bad code to rewrite, look later
	internal static IEnumerable<string> GetParts(string input)
	{
		var parts = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
		for (var i = 0; i < parts.Length; i++)
		{
			if (parts[i].StartsWith('"'))
			{
				parts[i] = parts[i].TrimStart('"');
				for (var start = i++; i < parts.Length; i++)
				{
					if (parts[i].EndsWith('"'))
					{
						parts[i] = parts[i].TrimEnd('"');
						yield return string.Join(" ", parts[start..(i + 1)]);
						break;
					}
				}
			}
			else
			{
				yield return parts[i];
			}
		}
	}

	internal static void Reset()
	{
		// testability and a bunch of static crap, I know...
		Middlewares.Clear();
		Middlewares.AddRange(DEFAULT_MIDDLEWARES);
		_converters.Clear();
		_cache = new();
	}

	private class CommandCache
	{
		private static Dictionary<Type, HashSet<(string, int)>> _commandAssemblyMap = new();

		private Dictionary<string, Dictionary<int, ChatCommand>> _newCache = new();
		public void AddCommand(string key, ParameterInfo[] parameters, ChatCommand command)
		{
			var p = parameters.Length;
			var d = parameters.Where(p => p.HasDefaultValue).Count();
			if (!_newCache.ContainsKey(key))
			{
				_newCache.Add(key, new());
			}

			// somewhat lame datastructure but memory cheap and tiny for space of commands
			for (var i = (p - d); i <= p; i++)
			{
				_newCache[key] = _newCache.GetValueOrDefault(key, new()) ?? new();
				if (_newCache[key].ContainsKey(i))
				{
					Log.Warning($"Command {key} has multiple commands with {i} parameters");
					continue;
				}
				_newCache[key][i] = command;
				var typeKey = command.Method.DeclaringType;

				var usedParams = _commandAssemblyMap.TryGetValue(typeKey, out var existing) ? existing : new();
				usedParams.Add((key, i));
				_commandAssemblyMap[typeKey] = usedParams;
			}
		}

		public (ChatCommand command, string[] args) GetCommand(string rawInput)
		{
			// todo: I think allows for overlap between .foo "bar" and .foo bar <no parameters>
			foreach (var (key, argCounts) in _newCache)
			{
				if (rawInput.StartsWith(key))
				{
					var remainder = rawInput.Substring(key.Length).Trim();
					var parameters = GetParts(remainder).ToArray();
					if (argCounts.TryGetValue(parameters.Length, out var cmd))
					{
						return (cmd, parameters);
					}
				}
			}

			return (null, null);
		}

		public void RemoveCommandsFromType(Type t)
		{
			if (!_commandAssemblyMap.TryGetValue(t, out var commands))
			{
				return;
			}
			foreach (var (key, index) in commands)
			{
				if (!_newCache.TryGetValue(key, out var dict))
				{
					continue;
				}
				dict.Remove(index);
			}
			_commandAssemblyMap.Remove(t);
		}

		public void Clear()
		{
			_newCache.Clear();
		}

		internal void Reset()
		{
			throw new NotImplementedException();
		}
	}

	public record ChatCommand(ChatCommandAttribute Attribute, MethodInfo Method, ConstructorInfo Constructor, ParameterInfo[] Parameters, Type ContextType, Type ConstructorType);

	// todo: document this default behavior, it's just not something to ship without but you can Middlewares.Claer();
	private static List<CommandMiddleware> DEFAULT_MIDDLEWARES = new() { new VCF.Core.Basics.BasicAdminCheck() };
	public static List<CommandMiddleware> Middlewares { get; } = new() { new VCF.Core.Basics.BasicAdminCheck() };

	public static ChatCommand Handle(ICommandContext ctx, string input)
	{
		static bool HandleCanExecute(ICommandContext ctx, ChatCommand command)
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
		static void HandleBeforeExecute(ICommandContext ctx, ChatCommand command)
		{
			Middlewares.ForEach(m => m.BeforeExecute(ctx, command.Attribute, command.Method));
		}

		static void HandleAfterExecute(ICommandContext ctx, ChatCommand command)
		{
			Middlewares.ForEach(m => m.AfterExecute(ctx, command.Attribute, command.Method));
		}


		var (command, args) = _cache.GetCommand(input);

		if (command == null) return null; // NOT FOUND

		// Handle Context Type not matching command
		if (!command.ContextType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Id}] but can not assign {command.ContextType.Name} from {ctx?.GetType().Name}");
			return null;
		}

		// Then handle this invocation's context not being valid for the command classes custom constructor
		if (command.Constructor != null && !command.ConstructorType.IsAssignableFrom(ctx?.GetType()))
		{
			Log.Warning($"Matched [{command.Attribute.Id}] but can not assign {command.ConstructorType.Name} from {ctx?.GetType().Name}");
			return null;
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
				return null;
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
				catch (TargetInvocationException tie) when (tie.InnerException is ChatCommandException e)
				{
					// todo: error matched type but failed to convert arg to type
					ctx.Reply($"<color=red>[error]</color> Failed converted parameter: {e.Message}");
					return null;
				}
				catch (Exception e)
				{
					// todo: failed custom converter unhandled
					Log.Warning($"Hit unexpected exception {e}");
					throw e;
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
					return null;
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
			return null; // todo: need better return type
		}

		HandleBeforeExecute(ctx, command);

		// Execute Command
		try
		{
			command.Method.Invoke(instance, commandArgs);
		}
		catch (TargetInvocationException tie) when (tie.InnerException is ChatCommandException e)
		{
			ctx.Reply($"<color=red>[error]</color> {e.Message}");
			return null;
		}
		catch (Exception e)
		{
			Log.Warning($"Hit unexpected exception executing command {command.Attribute.Id}\n: {e}");
			throw e;
		}

		HandleAfterExecute(ctx, command);

		return command;
	}

	public static void RegisterConverter(Type converter)
	{
		// TODO: need to explicitly fail here, wtf you asking me to do if you aren't a converter

		// check base type
		if (converter.BaseType.Name != typeof(ChatCommandArgumentConverter<>).Name)
		{
			// can't bud
			Log.Error("wrong type");
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

	public static void RegisterAssembly(Assembly assembly, string assemblyPrefix = null)
	{
		var types = assembly.GetTypes();
		foreach (var type in types)
		{
			RegisterCommandType(type, assemblyPrefix);
		}
	}

	public static void RegisterCommandType(Type type, string assemblyPrefix = null)
	{
		var groupAttr = type.GetCustomAttribute<ChatCommandGroupAttribute>();
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
			RegisterMethod(assembly, assemblyPrefix, groupAttr, contextConstructor, method);
		}
	}

	private static void RegisterMethod(Assembly assembly, string assemblyPrefix, ChatCommandGroupAttribute groupAttr, ConstructorInfo customConstructor, MethodInfo method)
	{
		var commandAttr = method.GetCustomAttribute<ChatCommandAttribute>();
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

		var command = new ChatCommand(commandAttr, method, customConstructor, parameters, first.ParameterType, constructorType);

		// todo include prefix and group in here, this shoudl be a string match
		// todo handle collisons here

		// BAD CODE INC.. permute and cache keys -> command
		var groupNames = groupAttr == null ? new[] { "" } : groupAttr.ShortHand == null ? new[] { $"{groupAttr.Name} " } : new[] { $"{groupAttr.Name} ", $"{groupAttr.ShortHand} ", };
		var names = commandAttr.ShortHand == null ? new[] { commandAttr.Name } : new[] { commandAttr.Name, commandAttr.ShortHand };
		var prefix = groupAttr?.Prefix ?? assemblyPrefix ?? DEFAULT_PREFIX; // TODO: get from attribute/config
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
