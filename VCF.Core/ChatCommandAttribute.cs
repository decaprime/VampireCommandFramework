using System;

namespace VampireCommandFramework
{
	public class ChatCommandAttribute : Attribute
	{
		public ChatCommandAttribute(string name, string shortHand = null, string usage = null, string description = null, string id = null)
		{
			Name = name;
			ShortHand = shortHand;
			Usage = usage;
			Description = description;
			Id = id ?? Name.Replace(" ","-");
		}

		public string Name { get; }
		public string ShortHand { get; }
		public string Usage { get; }
		public string Description { get; }
		public string Id { get; }
	}
}
