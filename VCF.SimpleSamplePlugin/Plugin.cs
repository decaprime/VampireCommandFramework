using BepInEx;
using BepInEx.IL2CPP;
using VampireCommandFramework;
using VampireCommandFramework.Registry;
using PluginInfo = VCF.SimpleSamplePlugin.MyPluginInfo;

namespace VCF.SimpleSamplePlugin;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
internal class Plugin : BasePlugin
{
	public override void Load()
	{
		Log.LogDebug("Simple Plugin Loaded");
		CommandRegistry.RegisterAll(typeof(SimplePluginCommands).Assembly);
	}

	public override bool Unload()
	{
		return true;
	}
	public static int Counter = 0;

}

public class SimplePluginCommands
{
	[Command("ping")]
	public void Ping(ICommandContext ctx, int num = 5) => ctx.Reply($"pong Counter={Plugin.Counter += num}");
}
