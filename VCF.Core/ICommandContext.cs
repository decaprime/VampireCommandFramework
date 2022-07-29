using ProjectM.Network;
using System;
using Wetstone.Hooks;

namespace VampireCommandFramework
{
	public interface ICommandContext
	{
		IServiceProvider Services { get; }
		User User { get; }

		ChatCommandException Error(string LogMessage);
		void Reply(string v);
	}
}