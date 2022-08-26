using System;
using System.Runtime.Serialization;

namespace VampireCommandFramework;

[Serializable]
public class ChatCommandException : Exception
{
	public ChatCommandException()
	{
	}

	public ChatCommandException(string message) : base(message)
	{
	}

	public ChatCommandException(string message, Exception innerException) : base(message, innerException)
	{
	}

	protected ChatCommandException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}