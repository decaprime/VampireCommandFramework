using System;

namespace VampireCommandFramework;

public class CommandAttribute : Attribute
{
	public CommandAttribute(string name, string shortHand = null, string usage = null, string description = null, string id = null, bool adminOnly = false)
	{
		Name = name;
		ShortHand = shortHand;
		Usage = usage;
		Description = description;
		Id = id ?? Name.Replace(" ", "-");
		AdminOnly = adminOnly;
	}

	public string Name { get; }
	public string ShortHand { get; }
	public string Usage { get; }
	public string Description { get; }
	public string Id { get; }
	public bool AdminOnly { get; }
}
