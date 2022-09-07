using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
				PrintAssemblyHelp(foundAssembly);
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

				foreach (var command in individualResults)
				{
					PrintCommandHelp(command.Key, command.Value);
				}

				if (!individualResults.Any())
				{
					ctx.SysReply($"Could not find any commands for \"{search}\"");
				}
			}
		}
		else
		{
			ctx.SysReply($"Listing {B("all")} commands");
			foreach (var assembly in CommandRegistry.AssemblyCommandMap)
			{
				PrintAssemblyHelp(assembly);
			}
		}

		void PrintAssemblyHelp(KeyValuePair<Assembly, Dictionary<CommandMetadata, List<string>>> assembly)
		{
			ctx.SysReply($"Commands from {B(assembly.Key.GetName().Name)}:");
			foreach (var command in assembly.Value.Keys)
			{
				ctx.SysReply(GenerateHelpText(command));
			}
		}

		void PrintCommandHelp(CommandMetadata command, List<string> aliases)
		{

			ctx.SysReply($"{B(command.Attribute.Name)} ({command.Attribute.Id}) {command.Attribute.Description}");
			ctx.SysReply(GenerateHelpText(command));
			ctx.SysReply($"Aliases: {string.Join(", ", aliases).Italic()}");
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

