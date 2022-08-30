using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;

namespace VampireCommandFramework;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class Plugin : BasePlugin
{
	private Harmony _harmony;

	public override void Load()
	{
		VampireCommandFramework.Log.Instance = Log;
		// Plugin startup logic
		_harmony = new Harmony(PluginInfo.PLUGIN_GUID);
		_harmony.PatchAll();

		IL2CPPChainloader.Instance.Plugins.TryGetValue(PluginInfo.PLUGIN_GUID, out var info);
		Log.LogMessage($"VCF Loaded: {info?.Metadata.Version}");
	}

	public override bool Unload()
	{
		_harmony.UnpatchSelf();
		return true;
	}
}