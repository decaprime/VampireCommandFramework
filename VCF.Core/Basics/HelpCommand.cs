using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VampireCommandFramework.Common;
using VampireCommandFramework.Registry;

using static VampireCommandFramework.Format;

namespace VampireCommandFramework.Basics;

internal static class HelpCommands
{
	private static readonly Regex _trailingLongDashRegex = new Regex(@"-\d+$");

	[Command("help-legacy", description: "Passes through a .help command that is compatible with other mods that don't use VCF.")]
	public static void HelpLegacy(ICommandContext ctx, string search = null) => ctx.SysReply($"Attempting compatible .help {search} for non-VCF mods.");

	[Command("help")]
	public static void HelpCommand(ICommandContext ctx, string search = null, string filter = null)
	{
		// If search is specified first look for matching assembly, then matching command
		if (!string.IsNullOrEmpty(search))
		{
			var foundAssembly = CommandRegistry.AssemblyCommandMap.FirstOrDefault(x => x.Key.GetName().Name.StartsWith(search, StringComparison.OrdinalIgnoreCase));
			if (foundAssembly.Value != null)
			{
				StringBuilder sb = new();
				PrintAssemblyHelp(ctx, foundAssembly, sb, filter);
				ctx.SysPaginatedReply(sb);
			}
			else
			{
				// if we fail to find we assume your query is matching a command, look through all of the commands
				var commands = CommandRegistry.AssemblyCommandMap.SelectMany(x => x.Value);//.Select(x => (x.Key, x.Value));
				var individualResults = commands.Where(x =>
					string.Equals(x.Key.Attribute.Id, search, StringComparison.InvariantCultureIgnoreCase)
					|| string.Equals(x.Key.Attribute.Name, search, StringComparison.InvariantCultureIgnoreCase)
					|| x.Value.Contains(search, StringComparer.InvariantCultureIgnoreCase)
				);

				individualResults = individualResults.Where(kvp => CommandRegistry.CanCommandExecute(ctx, kvp.Key))
					                                 .Where(kvp => filter == null ||
													               kvp.Key.Attribute.Name.Contains(filter, StringComparison.InvariantCultureIgnoreCase));

				if (!individualResults.Any())
				{
					throw ctx.Error($"Could not find any commands for \"{search}\"");
				}

				var sb = new StringBuilder();
				foreach (var command in individualResults)
				{
					GenerateFullHelp(command.Key, command.Value, sb);
				}

				ctx.SysPaginatedReply(sb);
			}
		}
		else
		{
			var sb = new StringBuilder();
			sb.AppendLine($"Listing {B("all")} plugins");
			sb.AppendLine($"Use {B(".help <plugin>")} for commands in that plugin");
			// List all plugins they have a command they can execute for
			foreach (var assemblyName in CommandRegistry.AssemblyCommandMap.Where(x => x.Value.Keys.Any(c => CommandRegistry.CanCommandExecute(ctx, c)))
																		   .Select(x => x.Key.GetName().Name)
																		   .OrderBy(x => x))
			{
				sb.AppendLine($"{assemblyName}");
			}
			ctx.SysPaginatedReply(sb);
		}

		void GenerateFullHelp(CommandMetadata command, List<string> aliases, StringBuilder sb)
		{
			sb.AppendLine($"{B(command.Attribute.Name)} ({command.Attribute.Id}) {command.Attribute.Description}");
			sb.AppendLine(PrintShortHelp(command));
			sb.AppendLine($"{B("Aliases").Underline()}: {string.Join(", ", aliases).Italic()}");

			// Automatically Display Enum types
			var enums = command.Parameters.Select(p => p.ParameterType).Distinct().Where(t => t.IsEnum);
			foreach (var e in enums)
			{
				sb.AppendLine($"{Format.Bold($"{e.Name} Values").Underline()}: {string.Join(", ", Enum.GetNames(e))}");
			}

			// Check CommandRegistry for types that can be converted and further for IConverterUsage
			var converters = command.Parameters.Select(p => p.ParameterType).Distinct().Where(p => CommandRegistry._converters.ContainsKey(p));
			foreach (var c in converters)
			{
				var (obj, _, _) = CommandRegistry._converters[c];
				if (obj is not IConverterUsage) continue;
				IConverterUsage converterUsage = obj as IConverterUsage;

				sb.AppendLine($"{Format.Bold($"{c.Name}")}: {converterUsage.Usage}");
			}
		}
	}

	[Command("help-all", description: "Returns all plugin commands")]
	public static void HelpAllCommand(ICommandContext ctx, string filter = null)
	{
		var sb = new StringBuilder();
		if (filter == null)
			sb.AppendLine($"Listing {B("all")} commands");
		else
			sb.AppendLine($"Listing {B("all")} commands matching filter '{filter}'");
		
		var foundAnything = false;
		foreach (var assembly in CommandRegistry.AssemblyCommandMap.Where(x => x.Value.Keys.Any(c => CommandRegistry.CanCommandExecute(ctx, c) &&
																										 (filter == null ||
																										  PrintShortHelp(c).Contains(filter, StringComparison.InvariantCultureIgnoreCase)))))
		{
			PrintAssemblyHelp(ctx, assembly, sb, filter);
			foundAnything = true;
		}

		if (!foundAnything)
			throw ctx.Error($"Could not find any commands for \"{filter}\"");

		ctx.SysPaginatedReply(sb);
	}

	static void PrintAssemblyHelp(ICommandContext ctx, KeyValuePair<Assembly, Dictionary<CommandMetadata, List<string>>> assembly, StringBuilder sb, string filter = null)
	{
		var name = assembly.Key.GetName().Name;
		name = _trailingLongDashRegex.Replace(name, "");

		sb.AppendLine($"Commands from {name.Medium().Color(Color.Primary)}:".Underline());
		var commands = assembly.Value.Keys.Where(c => CommandRegistry.CanCommandExecute(ctx, c));

		foreach (var command in commands.OrderBy(c => (c.GroupAttribute != null ? c.GroupAttribute.Name + " " : "") + c.Attribute.Name))
		{
			var helpLine = PrintShortHelp(command);
			if (filter == null || helpLine.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
				sb.AppendLine(helpLine);
		}
	}

	internal static string PrintShortHelp(CommandMetadata command)
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

