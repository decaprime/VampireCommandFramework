using FakeItEasy;
using NUnit.Framework;
using System.Text;
using VampireCommandFramework;
using VampireCommandFramework.Registry;

namespace VCF.Tests;

public class OverloadTests
{
	internal static bool IsFirstCalled = false;
	internal static bool IsSecondCalled = false;
	private readonly ICommandContext AnyCtx = A.Fake<ICommandContext>();

	public class OverloadTestCommands
	{
		[Command("overload", usage: "no-arg")]
		public void Overload(ICommandContext ctx)
		{
			IsFirstCalled = true;
		}

		[Command("overload", usage: "one-arg")]
		public void Overload(ICommandContext ctx, string arg)
		{
			IsSecondCalled = true;
		}

		[Command("nooverload", usage: "no-arg")]
		public void Overload2(ICommandContext ctx) { }
	}

	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterCommandType(typeof(OverloadTestCommands));
		IsFirstCalled = false;
		IsSecondCalled = false;
	}


	[Test]
	public void CanOverload_CallFirstCommand()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".overload"), Is.EqualTo(CommandResult.Success));
		Assert.IsTrue(IsFirstCalled);
		Assert.IsFalse(IsSecondCalled);
	}

	[Test]
	public void CanOverload_CallSecondCommand()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".overload test"), Is.EqualTo(CommandResult.Success));
		Assert.IsFalse(IsFirstCalled);
		Assert.IsTrue(IsSecondCalled);
	}

	[Test]
	public void Overload_PartialMatch_ListsAll()
	{
		StringBuilder sb = new();
		A.CallTo(() => AnyCtx.Reply(A<string>._)).Invokes((string s) => sb.AppendLine(s));
		// Todo build TestContext that lets you assert on replys/errors.


		var result = CommandRegistry.Handle(AnyCtx, ".overload test test");
		Assert.That(result, Is.EqualTo(CommandResult.UsageError));
		Assert.That(sb.ToString(), Is.EqualTo($"""
			overload no-arg (todo)
			overload one-arg (todo)

			"""));
	}

	[Test]
	public void Nooverload_PartialMatch_ListsOne()
	{
		StringBuilder sb = new();
		A.CallTo(() => AnyCtx.Reply(A<string>._)).Invokes((string s) => sb.AppendLine(s));

		var result = CommandRegistry.Handle(AnyCtx, ".nooverload test test");
		Assert.That(result, Is.EqualTo(CommandResult.UsageError));
		Assert.That(sb.ToString(), Is.EqualTo($"""
			nooverload no-arg (todo)

			"""));
	}
}
