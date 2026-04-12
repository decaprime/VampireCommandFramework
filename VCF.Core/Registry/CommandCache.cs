using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Registry;

internal class CommandCache
{
	private static Dictionary<Type, HashSet<(string, int)>> _commandAssemblyMap = new();

	internal Dictionary<string, Dictionary<int, List<CommandMetadata>>> _newCache = new();
	
	internal Dictionary<string, List<CommandMetadata>> _remainderCache = new();

	internal void AddCommand(string key, ParameterInfo[] parameters, CommandMetadata command)
	{
		key = key.ToLowerInvariant();
		var p = parameters.Length;
		var d = parameters.Where(p => p.HasDefaultValue).Count();
		
		bool hasRemainder = parameters.Length > 0 &&
							CommandRegistry.IsRemainderParameter(parameters[parameters.Length - 1]);
		
		if (!_newCache.ContainsKey(key))
		{
			_newCache.Add(key, new());
		}

		if (hasRemainder)
		{
			// Add to remainder cache
			if (!_remainderCache.ContainsKey(key))
			{
				_remainderCache[key] = new List<CommandMetadata>();
			}
			_remainderCache[key].Add(command);

			var typeKey = command.Method.DeclaringType;
			var usedParams = _commandAssemblyMap.TryGetValue(typeKey, out var existing) ? existing : new();
			usedParams.Add((key, -1)); // Use -1 to indicate remainder command in assembly map
			_commandAssemblyMap[typeKey] = usedParams;
		}
		else
		{
			// Original logic for non-remainder commands
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
	}

	internal CacheResult GetCommand(string rawInput)
	{
		var lowerRawInput = rawInput.ToLowerInvariant();
		List<CommandMetadata> possibleMatches = new();
		List<(CommandMetadata Command, string[] Args)> exactMatches = new();

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
						// Add all commands that match the exact parameter count, paired with their parameters
						foreach (var cmd in cmds)
						{
							exactMatches.Add((cmd, parameters));
						}
					}
					
					// Check remainder commands for this key
					if (_remainderCache.TryGetValue(key, out var remainderCommands))
					{
						foreach (var remainderCmd in remainderCommands)
						{
							// Check if this remainder command can handle the provided parameter count
							var remainderParams = remainderCmd.Method.GetParameters();
							var requiredParams = remainderParams.Count(p => !p.HasDefaultValue) - 2; // Exclude ctx and the [Remainder] parameter itself
							
							if (parameters.Length >= requiredParams)
							{
								exactMatches.Add((remainderCmd, parameters));
							}
						}
					}
					
					if (exactMatches.Count == 0)
					{
						// Add all possible matches for the command name but different param counts
						possibleMatches.AddRange(argCounts.Values.SelectMany(x => x));
						
						// Also add remainder commands as possible matches
						if (_remainderCache.TryGetValue(key, out var remainderCmds))
						{
							possibleMatches.AddRange(remainderCmds);
						}
					}
				}
			}
		}

		// If we have exact matches, return them
		if (exactMatches.Count > 0)
		{
			return new CacheResult(exactMatches, null);
		}

		// No exact matches found
		return new CacheResult(((CommandMetadata, string[])?)null, possibleMatches.Distinct());
	}

	// Handle assembly-specific command lookup
	internal CacheResult GetCommandFromAssembly(string rawInput, string assemblyName)
	{
		var lowerRawInput = rawInput.ToLowerInvariant();
		List<CommandMetadata> possibleMatches = new();
		List<(CommandMetadata Command, string[] Args)> exactMatches = new();

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
						// Add all commands that match the exact parameter count and assembly name, paired with their parameters
						foreach (var cmd in cmds.Where(cmd => cmd.AssemblyName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)))
						{
							exactMatches.Add((cmd, parameters));
						}
					}
					
					// Check remainder commands for this key and assembly
					if (_remainderCache.TryGetValue(key, out var remainderCommands))
					{
						foreach (var remainderCmd in remainderCommands.Where(cmd => cmd.AssemblyName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)))
						{
							// Check if this remainder command can handle the provided parameter count
							var remainderParams = remainderCmd.Method.GetParameters();
							var requiredParams = remainderParams.Count(p => !p.HasDefaultValue) - 2; // Exclude ctx and the [Remainder] parameter itself

							if (parameters.Length >= requiredParams)
							{
								exactMatches.Add((remainderCmd, parameters));
							}
						}
					}
					
					if (exactMatches.Count == 0)
					{
						// Add all possible matches for the command name but different param counts
						possibleMatches.AddRange(argCounts.Values.SelectMany(x => x)
						                                     .Where(cmd => cmd.AssemblyName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)));
						
						// Also add remainder commands as possible matches
						if (_remainderCache.TryGetValue(key, out var remainderCmds))
						{
							possibleMatches.AddRange(remainderCmds.Where(cmd => cmd.AssemblyName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)));
						}
					}
				}
			}
		}

		// If we have exact matches, return them
		if (exactMatches.Count > 0)
		{
			return new CacheResult(exactMatches, null);
		}

		// No exact matches found
		return new CacheResult(((CommandMetadata, string[])?)null, possibleMatches.Distinct());
	}

	internal void RemoveCommandsFromType(Type t)
	{
		if (!_commandAssemblyMap.TryGetValue(t, out var commands))
		{
			return;
		}
		foreach (var (key, index) in commands)
		{
			if (index == -1) // Remainder command
			{
				if (_remainderCache.TryGetValue(key, out var remainderList))
				{
					remainderList.RemoveAll(cmd => cmd.Method.DeclaringType == t);
					
					// If the list is now empty, remove the entry
					if (remainderList.Count == 0)
					{
						_remainderCache.Remove(key);
					}
				}
			}
			else // Regular command
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
		}
		_commandAssemblyMap.Remove(t);
	}

	internal void Clear()
	{
		_newCache.Clear();
		_remainderCache.Clear();
	}

	internal void Reset()
	{
		throw new NotImplementedException();
	}
}
