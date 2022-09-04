using System;

namespace VampireCommandFramework;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandGroupAttribute : Attribute
{
	public CommandGroupAttribute(string name, string shortHand = null)
	{
		Name = name;
		ShortHand = shortHand;
	}

	public string Name { get; }
	public string ShortHand { get; }
}
