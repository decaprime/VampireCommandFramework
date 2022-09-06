using System.Linq;
using VampireCommandFramework.Registry;

namespace VampireCommandFramework.Basics;

internal static class HelpCommands
{
	//[Command("help")]
	//public static void HelpCommand(ICommandContext ctx, string searchString = null)
	//{
	//}

	internal static string GenerateHelpText(CommandMetadata command)
	{
		var attr = command.Attribute;
		var groupPrefix = string.IsNullOrEmpty(command.GroupAttribute?.Name) ? string.Empty : $"{command.GroupAttribute.Name} ";
		var fullCommandName = groupPrefix + attr.Name;

		// Generate usage text automatically
		string usageText = GetOrGenerateUsage(command);

		var prefix = "[vcf] ".Color(Color.Primary) + CommandRegistry.DEFAULT_PREFIX.Color(Color.Yellow);
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
