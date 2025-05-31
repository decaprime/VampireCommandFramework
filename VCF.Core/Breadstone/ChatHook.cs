using ProjectM.Network;
using ProjectM;
using Unity.Entities;
using HarmonyLib;
using Unity.Collections;
using System;
using System.Linq;
using VampireCommandFramework.Common;
using System.Text;

namespace VampireCommandFramework.Breadstone;

[HarmonyPriority(200)]
[HarmonyBefore("gg.deca.Bloodstone")]
[HarmonyPatch(typeof(ChatMessageSystem), nameof(ChatMessageSystem.OnUpdate))]
public static class ChatMessageSystem_Patch
{
	public static void Prefix(ChatMessageSystem __instance)
	{
		if (__instance.__query_661171423_0 != null)
		{
			NativeArray<Entity> entities = __instance.__query_661171423_0.ToEntityArray(Allocator.Temp);
			foreach (var entity in entities)
			{
				var fromData = __instance.EntityManager.GetComponentData<FromCharacter>(entity);
				var userData = __instance.EntityManager.GetComponentData<User>(fromData.User);
				var chatEventData = __instance.EntityManager.GetComponentData<ChatMessageEvent>(entity);

				var messageText = chatEventData.MessageText.ToString();

				if (!messageText.StartsWith(".") || messageText.StartsWith("..")) continue;

				VChatEvent ev = new VChatEvent(fromData.User, fromData.Character, messageText, chatEventData.MessageType, userData);
				var ctx = new ChatCommandContext(ev);

				CommandResult result;
				try
				{
					result = CommandRegistry.Handle(ctx, messageText);
				}
				catch (Exception e)
				{
					Log.Error($"Error while handling chat message {e}");
					continue;
				}

				// Legacy .help pass through support
				if (result == CommandResult.Success && messageText.StartsWith(".help-legacy", System.StringComparison.InvariantCulture))
				{
					chatEventData.MessageText = messageText.Replace("-legacy", string.Empty);
					__instance.EntityManager.SetComponentData(entity, chatEventData);
					continue;
				}
				else if (result == CommandResult.Unmatched)
				{
					var sb = new StringBuilder();

					sb.AppendLine($"Command not found: {messageText.Color(Color.Command)}");

					var closeMatches = CommandRegistry.FindCloseMatches(ctx, messageText).ToArray();
					if (closeMatches.Length > 0)
					{
						sb.AppendLine($"Did you mean: {string.Join(", ", closeMatches.Select(c => c.Color(Color.Command)))}");
					}
					ctx.SysReply(sb.ToString());
				}
				VWorld.Server.EntityManager.DestroyEntity(entity);
			}
		}
	}
}