using System;
using System.Collections.Generic;
using System.Linq;

namespace VampireCommandFramework.Registry;

internal record CacheResult
{
	internal IEnumerable<(CommandMetadata Command, string[] Args)> Commands { get; }
	internal IEnumerable<CommandMetadata> PartialMatches { get; }

	internal bool IsMatched => Commands != null && Commands.Any();
	internal bool HasPartial => PartialMatches?.Any() ?? false;

	// Constructor for multiple commands
	public CacheResult(IEnumerable<(CommandMetadata Command, string[] Args)> commands, IEnumerable<CommandMetadata> partialMatches)
	{
		Commands = commands;
		PartialMatches = partialMatches;
	}

	// Constructor for single command or null
	public CacheResult((CommandMetadata Command, string[] Args)? command, IEnumerable<CommandMetadata> partialMatches)
	{
		Commands = command.HasValue ? new[] { command.Value } : null;
		PartialMatches = partialMatches;
	}
}
