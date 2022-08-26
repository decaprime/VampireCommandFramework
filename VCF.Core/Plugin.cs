using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;

namespace VampireCommandFramework;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("xyz.molenzwiebel.wetstone")]
[Wetstone.API.Reloadable]
internal class Plugin : BasePlugin
{
	private Harmony _harmony;

	public override void Load()
	{
		VampireCommandFramework.Log.Instance = Log;
		// Plugin startup logic
		_harmony = new Harmony(PluginInfo.PLUGIN_GUID);
		_harmony.PatchAll();

		Wetstone.Hooks.Chat.OnChatMessage += Chat_OnChatMessage;

		IL2CPPChainloader.Instance.Plugins.TryGetValue(PluginInfo.PLUGIN_GUID, out var info);
		Log.LogMessage($"VCF Loaded: {info?.Metadata.Version}");
	}

	private void Chat_OnChatMessage(Wetstone.Hooks.VChatEvent e)
	{
		var ctx = new CommandContext(e);
		CommandRegistry.Handle(ctx, e.Message);
	}

	public override bool Unload()
	{
		_harmony.UnpatchSelf();
		Wetstone.Hooks.Chat.OnChatMessage -= Chat_OnChatMessage;
		return true;
	}
}