using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;

public class StaticCommandsTests
{
	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
	}

	[Test]
	public void Static_Class_Static_Method_Command()
	{
		CommandRegistry.RegisterCommandType(typeof(StaticClassTestCommands));
		Assert.IsNotNull(CommandRegistry.Handle(A.Fake<ICommandContext>(), ".test"));
	}

	[Test]
	public void Regular_Class_Static_Method_Command()
	{
		CommandRegistry.RegisterCommandType(typeof(RegularClassTestCommands));
		Assert.IsNotNull(CommandRegistry.Handle(A.Fake<ICommandContext>(), ".test"));
	}

	public static class StaticClassTestCommands
	{
		[ChatCommand("test")]
		public static void Test(ICommandContext ctx) { }
	}

	public class RegularClassTestCommands
	{
		public RegularClassTestCommands(ICommandContext ctx)
		{
			Assert.Fail("Should not run this.");
		}

		[ChatCommand("test")]
		public static void Test(ICommandContext ctx) { }
	}
}
