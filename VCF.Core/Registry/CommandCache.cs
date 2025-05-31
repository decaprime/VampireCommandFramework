using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Registry;

internal class CommandCache
{
	private static Dictionary<Type, HashSet<(string, int)>> _commandAssemblyMap = new();

	// Change dictionary value from CommandMetadata to List<CommandMetadata>
	internal Dictionary<string, Dictionary<int, List<CommandMetadata>>> _newCache = new();

	internal void AddCommand(string key, ParameterInfo[] parameters, CommandMetadata command)
	{
		key = key.ToLowerInvariant();
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
			if (!_newCache[key].ContainsKey(i))
			{
				_newCache[key][i] = new List<CommandMetadata>();
			}

			// Add new command to the list
			_newCache[key][i].Add(command);

			var typeKey = command.Method.DeclaringType;

			var usedParams = _commandAssemblyMap.TryGetValue(typeKey, out var existing) ? existing : new();
			usedParams.Add((key, i));
			_commandAssemblyMap[typeKey] = usedParams;
		}
	}

	internal CacheResult GetCommand(string rawInput)
	{
		var lowerRawInput = rawInput.ToLowerInvariant();
		List<CommandMetadata> possibleMatches = new();
		List<CommandMetadata> exactMatches = new();

		foreach (var (key, argCounts) in _newCache)
		{
			if (lowerRawInput.StartsWith(key))
			{
				// Check if it's an exact match (no additional text) or if the next character is a space
				bool isExactMatch = lowerRawInput.Length == key.Length;
				bool hasSpaceAfter = lowerRawInput.Length > key.Length && lowerRawInput[key.Length] == ' ';

				if (isExactMatch || hasSpaceAfter)
				{
					string remainder = isExactMatch ? "" : rawInput.Substring(key.Length).Trim();
					string[] parameters = remainder.Length > 0 ? Utility.GetParts(remainder).ToArray() : Array.Empty<string>();

					if (argCounts.TryGetValue(parameters.Length, out var cmds))
					{
						// Add all commands that match the exact parameter count
						exactMatches.AddRange(cmds);

						// Store the parameters to return
						if (exactMatches.Count > 0 && parameters.Length > 0)
						{
							return new CacheResult(exactMatches, parameters, null);
						}
						else
						{
							return new CacheResult(exactMatches, Array.Empty<string>(), null);
						}
					}
					else
					{
						// Add all possible matches for the command name but different param counts
						possibleMatches.AddRange(argCounts.Values.SelectMany(x => x));
					}
				}
			}
		}

		// If we have exact matches but didn't return early
		if (exactMatches.Count > 0)
		{
			return new CacheResult(exactMatches, Array.Empty<string>(), null);
		}

		// Use the explicit single command constructor with null
		CommandMetadata nullCommand = null;
		return new CacheResult(nullCommand, null, possibleMatches.Distinct());
	}

	// Handle assembly-specific command lookup
	internal CacheResult GetCommandFromAssembly(string rawInput, string assemblyName)
	{
		var lowerRawInput = rawInput.ToLowerInvariant();
		List<CommandMetadata> possibleMatches = new();
		List<CommandMetadata> exactMatches = new();

		foreach (var (key, argCounts) in _newCache)
		{
			if (lowerRawInput.StartsWith(key))
			{
				// Check if it's an exact match (no additional text) or if the next character is a space
				bool isExactMatch = lowerRawInput.Length == key.Length;
				bool hasSpaceAfter = lowerRawInput.Length > key.Length && lowerRawInput[key.Length] == ' ';

				if (isExactMatch || hasSpaceAfter)
				{
					string remainder = isExactMatch ? "" : rawInput.Substring(key.Length).Trim();
					string[] parameters = remainder.Length > 0 ? Utility.GetParts(remainder).ToArray() : Array.Empty<string>();

					if (argCounts.TryGetValue(parameters.Length, out var cmds))
					{
						// Add all commands that match the exact parameter count and assembly name
						exactMatches.AddRange(cmds.Where(cmd => cmd.Assembly.GetName().Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)));

						// Store the parameters to return
						if (exactMatches.Count > 0 && parameters.Length > 0)
						{
							return new CacheResult(exactMatches, parameters, null);
						}
						else
						{
							return new CacheResult(exactMatches, Array.Empty<string>(), null);
						}
					}
					else
					{
						// Add all possible matches for the command name but different param counts
						possibleMatches.AddRange(argCounts.Values.SelectMany(x => x)
							                                     .Where(cmd => cmd.Assembly.GetName().Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)));
					}
				}
			}
		}

		// If we have exact matches but didn't return early
		if (exactMatches.Count > 0)
		{
			return new CacheResult(exactMatches, Array.Empty<string>(), null);
		}

		// Use the explicit single command constructor with null
		CommandMetadata nullCommand = null;
		return new CacheResult(nullCommand, null, possibleMatches.Distinct());
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

			if (dict.TryGetValue(index, out var cmdList))
			{
				cmdList.RemoveAll(cmd => cmd.Method.DeclaringType == t);

				// If the list is now empty, remove the entry
				if (cmdList.Count == 0)
				{
					dict.Remove(index);
				}
			}
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
