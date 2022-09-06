using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Registry;

internal class CommandCache
{
	private static Dictionary<Type, HashSet<(string, int)>> _commandAssemblyMap = new();

	private Dictionary<string, Dictionary<int, CommandMetadata>> _newCache = new();

	internal void AddCommand(string key, ParameterInfo[] parameters, CommandMetadata command)
	{
		var p = parameters.Length;
		var d = parameters.Where(p => p.HasDefaultValue).Count();
		if (!_newCache.ContainsKey(key))
		{
			_newCache.Add(key, new());
		}

		// somewhat lame datastructure but memory cheap and tiny for space of commands
		for (var i = p - d; i <= p; i++)
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

	internal CacheResult GetCommand(string rawInput)
	{
		// todo: I think allows for overlap between .foo "bar" and .foo bar <no parameters>
		List<CommandMetadata> possibleMatches = new();
		foreach (var (key, argCounts) in _newCache)
		{
			if (rawInput.StartsWith(key))
			{
				var remainder = rawInput.Substring(key.Length).Trim();
				var parameters = Utility.GetParts(remainder).ToArray();
				if (argCounts.TryGetValue(parameters.Length, out var cmd))
				{
					return new CacheResult(cmd, parameters, null);
				}
				else
				{
					possibleMatches.AddRange(argCounts.Values);
				}
			}
		}

		return new CacheResult(null, null, possibleMatches.Distinct());
	}

	internal void RemoveCommandsFromType(Type t)
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

	internal void Clear()
	{
		_newCache.Clear();
	}

	internal void Reset()
	{
		throw new NotImplementedException();
	}
}
