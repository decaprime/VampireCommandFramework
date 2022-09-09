using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using VampireCommandFramework.Common;
using VampireCommandFramework.Registry;

using static VampireCommandFramework.Format;

namespace VampireCommandFramework.Basics;

internal static class HelpCommands
{
	[Command("help-legacy", description: "Passes through a .help command that is compatible with other mods that don't use VCF.")]
	public static void HelpLegacy(ICommandContext ctx, string search = null) => ctx.SysReply($"Attempting compatible .help {search} for non-VCF mods.");

	[Command("help")]
	public static void HelpCommand(ICommandContext ctx, string search = null)
	{
		// If search is specified first look for matching assembly, then matching command
		if (!string.IsNullOrEmpty(search))
		{
			var foundAssembly = CommandRegistry.AssemblyCommandMap.FirstOrDefault(x => string.Equals(search, x.Key.GetName().Name, StringComparison.OrdinalIgnoreCase));
			if (foundAssembly.Value != null)
			{
				StringBuilder sb = new();
				PrintAssemblyHelp(foundAssembly, sb);
				ctx.SysPaginatedReply(sb);
			}
			else
			{
				// if we fail to find we assume your query is matching a command, look through all of the commands
				var commands = CommandRegistry.AssemblyCommandMap.SelectMany(x => x.Value).Select(x => (x.Key, x.Value));
				var individualResults = commands.Where(x =>
					string.Equals(x.Key.Attribute.Id, search, StringComparison.InvariantCultureIgnoreCase)
					|| string.Equals(x.Key.Attribute.Name, search, StringComparison.InvariantCultureIgnoreCase)
					|| x.Value.Contains(search, StringComparer.InvariantCultureIgnoreCase)
				);


				if (!individualResults.Any())
				{
					throw ctx.Error($"Could not find any commands for \"{search}\"");
				}

				var sb = new StringBuilder();
				foreach (var command in individualResults)
				{
					PrintCommandHelp(command.Key, command.Value, sb);
				}
				ctx.SysPaginatedReply(sb);
			}
		}
		else
		{
			var sb = new StringBuilder();
			sb.AppendLine($"Listing {B("all")} commands");
			foreach (var assembly in CommandRegistry.AssemblyCommandMap)
			{
				PrintAssemblyHelp(assembly, sb);
			}
			ctx.SysPaginatedReply(sb);
		}

		void PrintAssemblyHelp(KeyValuePair<Assembly, Dictionary<CommandMetadata, List<string>>> assembly, StringBuilder sb)
		{
			var name = assembly.Key.GetName().Name;
			sb.AppendLine($"Commands from {name.Medium().Color(Color.Primary)}:".Underline());
			foreach (var command in assembly.Value.Keys)
			{
				sb.AppendLine(GenerateHelpText(command));
			}
		}

		void PrintCommandHelp(CommandMetadata command, List<string> aliases, StringBuilder sb)
		{

			sb.AppendLine($"{B(command.Attribute.Name)} ({command.Attribute.Id}) {command.Attribute.Description}");
			sb.AppendLine(GenerateHelpText(command));
			sb.AppendLine($"Aliases: {string.Join(", ", aliases).Italic()}");
		}
	}

	internal static string GenerateHelpText(CommandMetadata command)
	{
		var attr = command.Attribute;
		var groupPrefix = string.IsNullOrEmpty(command.GroupAttribute?.Name) ? string.Empty : $"{command.GroupAttribute.Name} ";
		var fullCommandName = groupPrefix + attr.Name;

		// Generate usage text automatically
		string usageText = GetOrGenerateUsage(command);

		var prefix = CommandRegistry.DEFAULT_PREFIX.Color(Color.Yellow);
		var commandString = fullCommandName.Color(Color.White);
		return $"{prefix}{commandString}{usageText}";
	}

	internal static string GetOrGenerateUsage(CommandMetadata command)
	{
		var usageText = command.Attribute.Usage;
		if (string.IsNullOrWhiteSpace(usageText))
		{
			var usages = command.Parameters.Select(
				p => !p.HasDefaultValue
					? $"({p.Name})".Color(Color.LightGrey) // todo could compress this for the cases with no defaulting
					: $"[{p.Name}={p.DefaultValue}]".Color(Color.DarkGreen)
			);

			usageText = string.Join(" ", usages);
		}
		usageText = !string.IsNullOrWhiteSpace(usageText) ? $" {usageText}" : string.Empty;
		return usageText;
	}
}

