using System;
using System.Collections.Generic;

namespace VampireCommandFramework.Common;

internal static class Utility
{

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

	internal static void SysReply(this ICommandContext ctx, string input) => ctx.Reply($"[vcf] ".Color(Color.Primary) + input.Color(Color.White));
}