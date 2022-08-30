using ProjectM.Network;
using System;

namespace VampireCommandFramework;

public interface ICommandContext
{
	IServiceProvider Services { get; }
	
	string Name { get; }

	bool IsAdmin { get; }

	CommandException Error(string LogMessage);
	void Reply(string v);
}