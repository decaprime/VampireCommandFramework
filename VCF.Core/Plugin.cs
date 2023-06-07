using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ProjectM;
using ProjectM.Audio;
using System;
using System.Reflection;
using Unity.Entities;
using Unity.Transforms;
using VampireCommandFramework.Basics;

namespace VampireCommandFramework;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class Plugin : BasePlugin
{
	private Harmony _harmony;

	public override void Load()
	{
		Common.Log.Instance = Log;

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


		IL2CPPChainloader.Instance.Plugins.TryGetValue(PluginInfo.PLUGIN_GUID, out var info);
		Log.LogMessage($"VCF Loaded: {info?.Metadata.Version}");
	}

	public override bool Unload()
	{
		_harmony.UnpatchSelf();
		return true;
	}
}