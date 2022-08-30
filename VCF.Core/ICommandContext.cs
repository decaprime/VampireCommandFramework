using ProjectM.Network;
using System;

namespace VampireCommandFramework;

public interface ICommandContext
{
	IServiceProvider Services { get; }
	
	string Name { get; }

	bool IsAdmin { get; }

	ChatCommandException Error(string LogMessage);
	void Reply(string v);
}