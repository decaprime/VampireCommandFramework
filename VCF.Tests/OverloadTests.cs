using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;

public class OverloadTests
{
	internal static bool IsFirstCalled = false;
	internal static bool IsSecondCalled = false;
	
	public class OverloadTestCommands
	{
		[ChatCommand("overload")]
		public void Overload(CommandContext ctx)
		{
			IsFirstCalled = true;
		}

		[ChatCommand("overload")]
		public void Overload(CommandContext ctx, string arg)
		{
			IsSecondCalled = true;
		}
	}

	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		IsFirstCalled = false;
		IsSecondCalled = false;
	}


	[Test]
	public void CanOverload_CallFirstCommand()
	{
		CommandRegistry.RegisterCommandType(typeof(OverloadTestCommands));
		CommandRegistry.Handle(null, ".overload");
		Assert.IsTrue(IsFirstCalled);
		Assert.IsFalse(IsSecondCalled);
	}
	
	[Test]
	public void CanOverload_CallSecondCommand()
	{
		CommandRegistry.RegisterCommandType(typeof(OverloadTestCommands));
		CommandRegistry.Handle(null, ".overload test");
		Assert.IsFalse(IsFirstCalled);
		Assert.IsTrue(IsSecondCalled);
	}
}
