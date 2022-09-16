using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;
public static class TestUtilities
{
	public static void AssertHandle(string command, CommandResult result, string? withReply = null)
	{
		AssertReplyContext ctx = new();
		AssertHandle(ctx, command, result);
		if (!string.IsNullOrEmpty(withReply))
		{
			ctx.AssertReply(withReply);
		}
	}

	public static void AssertHandle(ICommandContext context, string command, CommandResult result)
	{
		Assert.That(CommandRegistry.Handle(context, command), Is.EqualTo(result));
	}
}
