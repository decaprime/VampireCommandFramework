using System;

namespace VampireCommandFramework
{
	public class ChatCommandAttribute : Attribute
	{
		public ChatCommandAttribute(string name, string shortHand = null, string usage = null, string description = null)
		{
			Name = name;
			ShortHand = shortHand;
			Usage = usage;
			Description = description;
		}

		public string Name { get; }
		public string ShortHand { get; }
		public string Usage { get; }
		public string Description { get; }
	}
}
