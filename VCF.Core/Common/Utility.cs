using System;
using System.Collections.Generic;
using System.Text;

namespace VampireCommandFramework.Common;

internal static class Utility
{


	/// <summary>
	/// This method splits the input string into parts based on spaces, removing whitespace,
	/// but preserving quoted strings as literal parts.
	/// </summary>
	/// <remarks>
	/// This should support escaping quotes with \
	/// </remarks>
	internal static List<string> GetParts(string input)
	{
		var parts = new List<string>();
		if (string.IsNullOrWhiteSpace(input)) return parts;

		bool inQuotes = false;
		var sb = new StringBuilder();

		for (int i = 0; i < input.Length; i++)
		{
			char ch = input[i];

			// Handle escaped quotes
			if (ch == '\\' && i + 1 < input.Length)
			{
				char nextChar = input[i + 1];
				if (nextChar == '"')
				{
					sb.Append(nextChar);
					i++; // Skip the escaped quote
					continue;
				}
			}

			if (ch == '"')
			{
				inQuotes = !inQuotes;
				continue;
			}

			if (ch == ' ' && !inQuotes)
			{
				if (sb.Length > 0)
				{
					parts.Add(sb.ToString());
					sb.Clear();
				}
			}
			else
			{
				sb.Append(ch);
			}
		}

		if (sb.Length > 0)
		{
			parts.Add(sb.ToString());
		}

		return parts;
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