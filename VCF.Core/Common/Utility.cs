using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

	internal static void InternalError(this ICommandContext ctx) => ctx.SysReply("An internal error has occurred.");

	internal static void SysReply(this ICommandContext ctx, string input) => ctx.Reply($"[vcf] ".Color(Color.Primary) + input.Color(Color.White));

	internal static void SysPaginatedReply(this ICommandContext ctx, StringBuilder input) => SysPaginatedReply(ctx, input.ToString());

	const int MAX_MESSAGE_SIZE = 508 - 26 - 2 - 20; // factor for SysReply and newlines

	internal static void SysPaginatedReply(this ICommandContext ctx, string input)
	{
		if (input.Length <= MAX_MESSAGE_SIZE)
		{
			SysReply(ctx, input);
			return;
		}

		var pages = SplitIntoPages(input);
		foreach (var page in pages)
		{
			var trimPage = page.TrimEnd('\n', '\r', ' ');
			trimPage = Environment.NewLine + trimPage;
			SysReply(ctx, trimPage);
		}
	}

	/// <summary>
	/// This method splits <paramref name="rawText"/> into pages of <paramref name="pageSize"/> max size
	/// </summary>
	/// <param name="rawText"></param>
	internal static string[] SplitIntoPages(string rawText, int pageSize = MAX_MESSAGE_SIZE)
	{
		var pages = new List<string>();
		var page = new StringBuilder();
		var rawLines = rawText.Split(Environment.NewLine); // todo: does this work on both platofrms?
		var lines = new List<string>();
		
		// process rawLines -> lines of length <= pageSize
		foreach (var line in rawLines)
		{
			if (line.Length > pageSize)
			{
				// split into lines of max size preferring to split on spaces
				var remaining = line;
				while (!string.IsNullOrWhiteSpace(remaining) && remaining.Length > pageSize)
				{
					// find the last space before the page size within 5% of pageSize buffer
					var splitIndex = remaining.LastIndexOf(' ', pageSize - (int)(pageSize * 0.05));
					if (splitIndex < 0)
					{
						splitIndex = Math.Min(pageSize - 1, remaining.Length);
					}

					lines.Add(remaining.Substring(0, splitIndex));
					remaining = remaining.Substring(splitIndex);
				}
				lines.Add(remaining);
			}
			else
			{
				lines.Add(line);
			}
		}
		
		// batch as many lines together into pageSize
		foreach (var line in lines)
		{
			if ((page.Length + line.Length) > pageSize)
			{
				pages.Add(page.ToString());
				page.Clear();
			}
			page.AppendLine(line);
		}
		if (page.Length > 0)
		{
			pages.Add(page.ToString());
		}
		return pages.ToArray();
	}
}