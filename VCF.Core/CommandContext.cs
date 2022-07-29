using ProjectM.Network;
using System;
using Wetstone.API;
using Wetstone.Hooks;

namespace VampireCommandFramework
{
	public class CommandContext : ICommandContext
	{
		public VChatEvent Event { get; }

		public CommandContext(VChatEvent e)
		{
			Event = e;
		}

		public User User => Event?.User;

		public IServiceProvider Services { get; }

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
}
