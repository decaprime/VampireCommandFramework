using VampireCommandFramework.Registry;

namespace VampireCommandFramework.Basics;

public static class HelpCommands
{
	//[Command("help")]
	//public static void HelpCommand(ICommandContext ctx, string searchString = null)
	//{
	//}

	internal static string GenerateHelpText(CommandMetadata command)
	{
		var attr = command.Attribute;
		return $"{attr.Name} {(attr.Usage ?? "<usage>")} (todo)";
	}
}
