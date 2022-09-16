using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;
using VampireCommandFramework.Registry;

namespace VCF.Tests;

public class OverloadTests
{
	internal static bool IsFirstCalled = false;
	internal static bool IsSecondCalled = false;
	private AssertReplyContext AnyCtx = new();

	public class OverloadTestCommands
	{
		[Command("overload", usage: "how you use it")]
		public void Overload(ICommandContext ctx)
		{
			IsFirstCalled = true;
		}

		[Command("overload")]
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
		Format.Mode = Format.FormatMode.None;
		AnyCtx = new();
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
		var result = CommandRegistry.Handle(AnyCtx, ".overload test test");
		Assert.That(result, Is.EqualTo(CommandResult.UsageError));
		AnyCtx.AssertReply($"""
			[vcf] .overload how you use it
			[vcf] .overload (arg)
			""");
	}

	[Test]
	public void Nooverload_PartialMatch_ListsOne()
	{
		var result = CommandRegistry.Handle(AnyCtx, ".nooverload test test");
		Assert.That(result, Is.EqualTo(CommandResult.UsageError));
		AnyCtx.AssertReply($"""
			[vcf] .nooverload no-arg
			""");
	}
}
