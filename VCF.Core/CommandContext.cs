using ProjectM.Network;
using System;
using Wetstone.API;
using Wetstone.Hooks;

namespace VampireCommandFramework;

internal class CommandContext : ICommandContext
{
	protected VChatEvent Event { get; }

	public CommandContext(VChatEvent e)
	{
		Event = e;
	}

	protected User User => Event?.User;

	public IServiceProvider Services { get; }

	public string Name => User?.CharacterName.ToString();

	public bool IsAdmin => User?.IsAdmin ?? false;

	public void Reply(string v)
	{
		User.SendSystemMessage(v);
	}

	// todo: expand this, just throw from here as void and build a handler that can message user/log.
	// note: return exception lets callers throw ctx.Error() and control flow is obvious 
	public ChatCommandException Error(string LogMessage)
	{
		return new ChatCommandException(LogMessage);
	}
}
