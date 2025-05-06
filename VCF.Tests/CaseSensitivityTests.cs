using FakeItEasy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;

namespace VCF.Tests;
public class CaseSensitivityTests
{
	static string? passedArgument;

	class CaseSensitivityTestCommands
	{
		[Command("test")]
		public void TestCommand(ICommandContext ctx) { }

		[Command("testUPPER")]
		public void TestWithefault(ICommandContext ctx) { }

		[Command("testStringArgument")]
		public void TestStringArgument(ICommandContext ctx, string arg) { passedArgument = arg; }
	}

	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterCommandType(typeof(CaseSensitivityTestCommands));
		passedArgument = null;
	}

	[Test]
	public void CanUseUpperCase()
	{
		Assert.That(CommandRegistry.Handle(A.Fake<ICommandContext>(), ".tEsT"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallUpperCaseCommandWithLowerCase()
	{
		Assert.That(CommandRegistry.Handle(A.Fake<ICommandContext>(), ".testupper"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void StringArgumentKeepsCase()
	{
		Assert.That(CommandRegistry.Handle(A.Fake<ICommandContext>(), ".teststringargument CaseSensitivity"), Is.EqualTo(CommandResult.Success));
		Assert.That(passedArgument, Is.EqualTo("CaseSensitivity"));
	}
}
