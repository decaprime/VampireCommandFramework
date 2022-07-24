using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace VampireCommandFramework
{
	// TODO: replace with 
	public static class Log
	{
		public static void Warning(string s) => Write("warning", s);
		public static void Error(string s) => Write("error", s);
		public static void Debug(string s) => Write("debug", s);

		private static void Write(string prefix, string content) => Console.WriteLine($"[{prefix}] {content}");
	}

	public static class CommandRegistry
	{
		public record ChatCommand(ChatCommandAttribute Attribute, MethodInfo Method, ConstructorInfo Constructor, ParameterInfo[] Parameters);
		private static Dictionary<Type, (object instance, MethodInfo tryParse)> _converters = new();
		private static Dictionary<string, ChatCommand> _commandCache = new();
		private static Dictionary<Assembly, HashSet<string>> _commandAssemblyMap = new();

		private const string DEFAULT_PREFIX = ".";
		
		public static List<CommandMiddleware> Middlewares { get; } = new();

		public static void Reset()
		{
			// testability and a bunch of static crap, I know...
			_converters.Clear();
			_commandCache.Clear();
			Middlewares.Clear();
		}


		public static ChatCommand Handle(CommandContext ctx, string input)
		{
			static bool HandleCanExecute(CommandContext ctx, ChatCommand command)
			{
				foreach (var middleware in Middlewares)
				{
					if (!middleware.CanExecute(ctx, command.Attribute, command.Method))
					{
						return false;
					}
				}
				return true;
			}

			// todo: rethink, maybe you only want 1 door here, people will confuse these and it's probably possible to collapse
			static void HandleBeforeExecute(CommandContext ctx, ChatCommand command)
			{
				Middlewares.ForEach(m => m.BeforeExecute(ctx, command.Attribute, command.Method));
			}

			static void HandleAfterExecute(CommandContext ctx, ChatCommand command)
			{
				Middlewares.ForEach(m => m.AfterExecute(ctx, command.Attribute, command.Method));
			}

			// now gonna linear scan my dictionary because we're doing the dumb thing first
			var matches = _commandCache.Where(kvp => input.StartsWith(kvp.Key));
			if (!matches.Any()) return null; // or todo: print help texts

			if (matches.Count() > 1)
			{
				// todo: abiguous match, print error
				return null;
			}

			var (prefix, command) = matches.Single();

			// todo: (not here) but we elsewhere assume name overloading like .net, this assumes single match, will collide currently

			var remainder = input.Substring(prefix.Length);
			remainder = remainder.Trim(' ');

			// todo: support quote encoding and don't use simple split

			var args = string.IsNullOrWhiteSpace(remainder) ? Array.Empty<string>() : remainder.Split(" ");

			var argCount = args.Length;
			var paramsCount = command.Parameters.Length;
			var commandArgs = new object[paramsCount + 1];
			commandArgs[0] = ctx;

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

			for (var i = 0; i < argCount; i++)
			{
				var param = command.Parameters[i];
				var arg = args[i];

				if (_converters.TryGetValue(param.ParameterType, out var customConverter))
				{
					var (converter, convertMethod) = customConverter;

					object result;
					var tryParseArgs = new object[] { ctx, arg, null /* out result*/ };
					try
					{
						result = convertMethod.Invoke(converter, tryParseArgs);
					}
					catch (Exception)
					{
						// todo: failed custom converter unhandled
						return null;
					}

					if (result is bool successful && successful)
					{
						commandArgs[i + 1] = tryParseArgs[2];
						continue;
					}
					else
					{
						// todo: error matched type but failed to convert arg to type
						return null;
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
						Log.Error($"Can't convert {arg}: {e}");
						return null;
					}
				}
			}

			// construct command class with context if declared
			var instance = command.Constructor == null ? Activator.CreateInstance(command.Method.DeclaringType) : command.Constructor.Invoke(new[] { ctx });

			if (!HandleCanExecute(ctx, command)) return null; // todo: need better return type
			HandleBeforeExecute(ctx, command);

			// run command method
			command.Method.Invoke(instance, commandArgs);

			HandleAfterExecute(ctx, command);

			return command;
		}

		public static void RegisterConverter(Type converter)
		{
			// check base type
			if (converter.BaseType.Name != typeof(ChatCommandArgumentConverter<>).Name)
			{
				// can't bud
				Log.Error("wrong type");
				return;
			}

			object converterInstance = Activator.CreateInstance(converter);
			MethodInfo methodInfo = converter.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
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

		public static void RegisterAssembly(Assembly assembly, string? assemblyPrefix = null)
		{
			var types = assembly.GetTypes();
			foreach (var type in types)
			{
				var groupAttr = type.GetCustomAttribute<ChatCommandGroupAttribute>();
				if (groupAttr != null)
				{
					// handle groups - IDK later
				}

				var methods = type.GetMethods();

				var contextConstructor = type.GetConstructor(new[] { typeof(CommandContext) });

				foreach (var method in methods)
				{
					// TODO: multiple attributes check
					var commandAttr = method.GetCustomAttribute<ChatCommandAttribute>();
					if (commandAttr == null) continue;

					// check for CommandContext as first argument to method
					var paramInfos = method.GetParameters();
					var first = paramInfos.FirstOrDefault();
					if (first == null || first.ParameterType != typeof(CommandContext))
					{
						Log.Error($"Method {method.Name} has no CommandContext as first argument");
						continue;
					}

					var canConvert = paramInfos.Skip(1).All(param =>
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

					if (canConvert)
					{
						var command = new ChatCommand(commandAttr, method, contextConstructor, paramInfos.Skip(1).ToArray());


						// todo include prefix and group in here, this shoudl be a  string match
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
								if (_commandCache.ContainsKey(key))
								{
									Log.Warning($"Failed to add '{key}, this was already in use.");
									continue;
								}
								_commandCache.Add(key, command);

								// maintain assembly map for removal
								if (_commandAssemblyMap.TryGetValue(assembly, out var commandKeys))
								{
									commandKeys.Add(key);
								}
								else
								{
									_commandAssemblyMap.Add(assembly, new HashSet<string> { key });
								}
							}
						}
					}
				}
			}
		}

		public static void UnregisterAssembly(Assembly assembly)
		{
			// todo: maintain a map from assembly -> command

			if (!_commandAssemblyMap.TryGetValue(assembly, out var commandKeys)) return;
			foreach (var key in commandKeys)
			{
				_commandCache.Remove(key);
			}

			_commandAssemblyMap.Remove(assembly);
		}
	}
}
