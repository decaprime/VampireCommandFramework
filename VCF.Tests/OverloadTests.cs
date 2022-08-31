using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;

public class OverloadTests
{
	internal static bool IsFirstCalled = false;
	internal static bool IsSecondCalled = false;
	private readonly ICommandContext AnyCtx = A.Fake<ICommandContext>();

	public class OverloadTestCommands
	{
		[Command("overload")]
		public void Overload(ICommandContext ctx)
		{
			IsFirstCalled = true;
		}

		[Command("overload")]
		public void Overload(ICommandContext ctx, string arg)
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
		CommandRegistry.Handle(AnyCtx, ".overload");
		Assert.IsTrue(IsFirstCalled);
		Assert.IsFalse(IsSecondCalled);
	}

	[Test]
	public void CanOverload_CallSecondCommand()
	{
		CommandRegistry.RegisterCommandType(typeof(OverloadTestCommands));
		CommandRegistry.Handle(AnyCtx, ".overload test");
		Assert.IsFalse(IsFirstCalled);
		Assert.IsTrue(IsSecondCalled);
	}
}
