using NUnit.Framework;
using VampireCommandFramework;

using static VCF.Tests.TestUtilities;
namespace VCF.Tests;

public class CommandOverlapTests
{
	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterCommandType(typeof(OverlapCommands));
	}

	internal class OverlapCommands
	{
		public enum Thing { foo, bar };

		[Command("foo")]
		public void Foo(ICommandContext ctx, Thing v) => ctx.Reply("foo");

		[Command("foo bar")]
		public void FooBar(ICommandContext ctx) => ctx.Reply("foo bar");

		[Command("bar foo")]
		public void BarFoo(ICommandContext ctx) => ctx.Reply("bar foo");

		[Command("bar")]
		public void Bar(ICommandContext ctx, Thing v) => ctx.Reply("bar");
	}

	[Test]
	public void Does_FooBar_Overload_In_Order()
	{
		Format.Mode = Format.FormatMode.None;
		AssertHandle(".foo", CommandResult.UsageError);
		AssertHandle(".bar", CommandResult.UsageError);
		AssertHandle(".foo foo", CommandResult.Success, withReply: "foo");
		AssertHandle(".foo bar", CommandResult.Success, withReply: "[vcf] Multiple commands match this input. Select one by typing .<#>:\n"+
			                                                       " .1 - VCF.Tests - foo \n"+
																   "   .foo (v)\n"+
																   " .2 - VCF.Tests - foo bar \n"+
																   "   .foo bar");
		AssertHandle(".bar bar", CommandResult.Success, withReply: "bar");
		AssertHandle(".bar foo", CommandResult.Success, withReply: "[vcf] Multiple commands match this input. Select one by typing .<#>:\n" +
																   " .1 - VCF.Tests - bar foo \n" +
																   "   .bar foo\n"+
																   " .2 - VCF.Tests - bar \n" +
																   "   .bar (v)");
	}
}