using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using System;
using Unity.Entities;
using VampireCommandFramework;
using ProjectM;
using Engine.Console;
using Wetstone.API;

namespace VampireCommandFramework
{
	[BepInPlugin(PLUGIN_ID, "Vampire Command Framework", "0.2.2")]
	[BepInDependency("xyz.molenzwiebel.wetstone")]
	[Wetstone.API.Reloadable]
	internal class Plugin : BasePlugin
	{
		const string PLUGIN_ID = "gg.deca.VampireCommandFramework";

		private Harmony _harmony;

		public override void Load()
		{
			VampireCommandFramework.Log.Instance = Log;
			// Plugin startup logic
			_harmony = new Harmony(PLUGIN_ID);
			_harmony.PatchAll();
			
			Wetstone.Hooks.Chat.OnChatMessage += Chat_OnChatMessage;

			IL2CPPChainloader.Instance.Plugins.TryGetValue(PLUGIN_ID, out var info);
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
}
