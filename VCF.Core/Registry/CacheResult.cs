using System.Collections.Generic;
using System.Linq;

namespace VampireCommandFramework.Registry;

internal record CacheResult(CommandMetadata Command, string[] Args, IEnumerable<CommandMetadata> PartialMatches)
{
	internal bool IsMatched => Command != null;
	internal bool HasPartial => PartialMatches?.Any() ?? false;
}
