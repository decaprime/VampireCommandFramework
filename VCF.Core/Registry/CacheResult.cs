using System;
using System.Collections.Generic;
using System.Linq;

namespace VampireCommandFramework.Registry;

internal record CacheResult
{
	internal IEnumerable<CommandMetadata> Commands { get; }
	internal string[] Args { get; }
	internal IEnumerable<CommandMetadata> PartialMatches { get; }

	internal bool IsMatched => Commands != null && Commands.Any();
	internal bool HasPartial => PartialMatches?.Any() ?? false;

	// Constructor for multiple commands
	public CacheResult(IEnumerable<CommandMetadata> commands, string[] args, IEnumerable<CommandMetadata> partialMatches)
	{
		Commands = commands;
		Args = args ?? Array.Empty<string>(); // Ensure Args is never null
		PartialMatches = partialMatches;
	}

	// Constructor for single command or null
	public CacheResult(CommandMetadata command, string[] args, IEnumerable<CommandMetadata> partialMatches)
	{
		Commands = command != null ? new[] { command } : null;
		Args = args ?? Array.Empty<string>(); // Ensure Args is never null
		PartialMatches = partialMatches;
	}
}
