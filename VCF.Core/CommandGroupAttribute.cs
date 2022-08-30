using System;

namespace VampireCommandFramework;

public class CommandGroupAttribute : Attribute
{
	public CommandGroupAttribute(string name, string shortHand = null, string prefix = null)
	{
		Name = name;
		ShortHand = shortHand;
		Prefix = prefix;
	}

	public string Name { get; }
	public string ShortHand { get; }
	public string Prefix { get; }
}
