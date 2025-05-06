using Engine.Console;
using ProjectM;
using ProjectM.Network;
using System;
using Unity.Collections;
using VampireCommandFramework.Breadstone;

namespace VampireCommandFramework;

/// <summary>
/// This context is built around the in game chat event.
/// If you want your commands to be reusable you should define them againts <see cref="ICommandContext"/>
/// </summary>
/// <remarks>
/// You would use this context primarily when you need to access the character or chat event directly.
/// </remarks>
/// <example>
/// public class MyCommands
/// {
///		[ChatCommand(".close"]
///		public void WhosClose(CommandContext ctx)
///		{
///			var characterEntity =  ctx.Event.SenderCharacterEntity
///			var (distance, name) = GetClosestTeammate(characterEntity);
///			ctx.Reply($"{name} is {distance} away")
///		}
/// }
/// </example>
public class ChatCommandContext : ICommandContext
{
	public VChatEvent Event { get; }

	public ChatCommandContext(VChatEvent e)
	{
		Event = e;
	}

	public User User => Event.User;

	public IServiceProvider Services { get; }

	public string Name => User.CharacterName.ToString();
	public bool IsAdmin => User.IsAdmin;

	// If a message is longer than this an exception gets thrown converting to FixedString512Bytes
	static int maxMessageLength = 509;
	public void Reply(string v)
	{
		if (v.Length > maxMessageLength)
		 	v = v[..maxMessageLength];

		FixedString512Bytes unityMessage = v;
		ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, User, ref unityMessage);
	}

	// todo: expand this, just throw from here as void and build a handler that can message user/log.
	// note: return exception lets callers throw ctx.Error() and control flow is obvious 
	public CommandException Error(string LogMessage)
	{
		return new CommandException(LogMessage);
	}
}