using ProjectM.Network;
using ProjectM;
using Unity.Entities;
using HarmonyLib;
using Unity.Collections;
using System;

namespace VampireCommandFramework.Breadstone;

[HarmonyPriority(200)]
[HarmonyPatch(typeof(ChatMessageSystem), nameof(ChatMessageSystem.OnUpdate))]
public static class ChatMessageSystem_Patch
{
	public static bool Prefix(ChatMessageSystem __instance)
	{
		if (__instance.__ChatMessageJob_entityQuery != null)
		{
			NativeArray<Entity> entities = __instance.__ChatMessageJob_entityQuery.ToEntityArray(Allocator.Temp);
			foreach (var entity in entities)
			{
				var fromData = __instance.EntityManager.GetComponentData<FromCharacter>(entity);
				var userData = __instance.EntityManager.GetComponentData<User>(fromData.User);
				var chatEventData = __instance.EntityManager.GetComponentData<ChatMessageEvent>(entity);

				var messageText = chatEventData.MessageText.ToString();

				VChatEvent ev = new VChatEvent(fromData.User, fromData.Character, messageText, chatEventData.MessageType, userData);
				var ctx = new ChatCommandContext(ev);

				CommandResult result;
				try
				{
					result = CommandRegistry.Handle(ctx, messageText);
				}
				catch (Exception e)
				{
					Common.Log.Error($"Error while handling chat message {e}");
					continue;
				}

				// Legacy .help pass through support
				if (result == CommandResult.Success && messageText.StartsWith(".help-legacy", System.StringComparison.InvariantCulture))
				{
					chatEventData.MessageText = messageText.Replace("-legacy", string.Empty);
					__instance.EntityManager.SetComponentData(entity, chatEventData);
					return true;
				}

				else if (result != CommandResult.Unmatched)
				{
					__instance.EntityManager.AddComponent<DestroyTag>(entity);
					return false;
				}

			}
		}
		return true;
	}
}
