using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using System;
using Unity.Entities;
using VampireCommandFramework;

namespace VampireCommandFramework
{
	[BepInPlugin("gg.deca.VampireCommandFramework", "Vampire Command Framework", "1.0.0.0")]
	[BepInDependency("xyz.molenzwiebel.wetstone")]
	[Wetstone.API.Reloadable]
	internal class Plugin : BasePlugin
	{
		public override void Load()
		{
			Wetstone.Hooks.Chat.OnChatMessage += Chat_OnChatMessage;
			// load some config file to let end users override things without recompiling plugins
		}

		private void Chat_OnChatMessage(Wetstone.Hooks.VChatEvent e)
		{
			var ctx = new CommandContext(e);
			CommandRegistry.Handle(ctx, e.Message);
		}

		public override bool Unload()
		{
			Wetstone.Hooks.Chat.OnChatMessage -= Chat_OnChatMessage;
			return true;
		}
	}
}
