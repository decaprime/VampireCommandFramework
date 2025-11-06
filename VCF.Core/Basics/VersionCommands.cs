using VampireCommandFramework.Common;

namespace VampireCommandFramework.Basics;

internal static class VersionCommands
{
	[Command("version", description: "Lists all installed plugins and their versions", adminOnly: true)]
	public static void VersionCommand(ICommandContext ctx)
	{
		// Get the user entity if this is a ChatCommandContext
		var userEntity = ctx is ChatCommandContext chatCtx ? chatCtx.Event.SenderUserEntity : default;
		
		ThunderstoreVersionChecker.ListAllPluginVersions(userEntity);
	}
}
