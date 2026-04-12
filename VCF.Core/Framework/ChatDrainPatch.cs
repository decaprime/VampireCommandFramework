using System;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using VampireCommandFramework.Breadstone;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Framework;

// Harmony wiring for ChatMessageQueue: installs the production SendSink and
// drains one queued message per user per server frame.
internal static class ChatDrainPatch
{
	public static void Install()
	{
		ChatMessageQueue.SendSink = static (userObj, message) =>
		{
			var user = (User)userObj;
			FixedString512Bytes unityMessage = message;
			ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, user, ref unityMessage);
		};
	}

	[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
	public static class DrainTick_Patch
	{
		[HarmonyPostfix]
		public static void Postfix()
		{
			try
			{
				ChatMessageQueue.DrainOneTick();
			}
			catch (Exception e)
			{
				Log.Error($"ChatMessageQueue.DrainOneTick failed: {e}");
			}
		}
	}
}
