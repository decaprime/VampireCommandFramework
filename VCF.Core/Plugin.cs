using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework.Common;
using System.Threading.Tasks;
using BepInEx.Configuration;

namespace VampireCommandFramework;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class Plugin : BasePlugin
{
	private Harmony _harmony;
	
	// Configuration
	private static ConfigEntry<bool> EnableVersionCheck;

	public override void Load()
	{
		Common.Log.Instance = Log;

		// Initialize configuration
		EnableVersionCheck = Config.Bind("Version Check", "EnableVersionCheck", true, 
			"Enable automatic checking for plugin updates on Thunderstore at startup");

		if (!Breadstone.VWorld.IsServer)
		{
			Log.LogMessage("Note: Vampire Command Framework is loading on the client but only adds functionality on the server at this time, seeing this message is not a problem or bug.");
			return;
		}
		
		// Plugin startup logic
		_harmony = new Harmony(PluginInfo.PLUGIN_GUID);
		_harmony.PatchAll();

		CommandRegistry.RegisterCommandType(typeof(Basics.HelpCommands));
		CommandRegistry.RegisterCommandType(typeof(Basics.BepInExConfigCommands));
		CommandRegistry.RegisterCommandType(typeof(Basics.RepeatCommands));


		IL2CPPChainloader.Instance.Plugins.TryGetValue(PluginInfo.PLUGIN_GUID, out var info);
		Log.LogMessage($"VCF Loaded: {info?.Metadata.Version}");
		
		// Check for plugin updates on Thunderstore after all plugins are loaded
		if (EnableVersionCheck.Value)
		{
			IL2CPPChainloader.Instance.Finished += () =>
			{
				_ = Task.Run(async () =>
				{
					try
					{
						await ThunderstoreVersionChecker.CheckAllPluginVersionsAsync();
					}
					catch (System.Exception ex)
					{
						Log.LogWarning($"Version check failed: {ex.Message}");
					}
				});
			};
		}
		else
		{
			Log.LogInfo("Plugin version checking is disabled. Enable it in the config if you want to check for updates.");
		}
	}

	public override bool Unload()
	{
		_harmony.UnpatchSelf();
		return true;
	}
}