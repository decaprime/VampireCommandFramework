using FakeItEasy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;
using VampireCommandFramework.Basics;
using VampireCommandFramework.Registry;

namespace VCF.Tests;
public class HelpTests
{
	private AssertReplyContext AnyCtx;

	record SomeType();

	static readonly SomeType returnedFromConverter = new();

	class SomeTypeConverter : CommandArgumentConverter<SomeType>, IConverterUsage
	{
		public string Usage => "TEST-SENTINEL";
		public override SomeType Parse(ICommandContext ctx, string input) => returnedFromConverter;
	}

	enum SomeEnum { A, B, C }

	class HelpTestCommands
	{
		[Command("test-help")]
		public void TestHelp(ICommandContext ctx, SomeEnum someEnum, SomeType? someType = null)
		{

		}
	}

	[SetUp]
	public void Setup()
	{
		AnyCtx = new();
		Format.Mode = Format.FormatMode.None;
		CommandRegistry.Reset();
		CommandRegistry.RegisterCommandType(typeof(HelpCommands));
	}

	[Test]
	public void HelpCommand_RegisteredByDefault()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help-legacy"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void HelpCommand_Help_ListsAll()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Listing all commands
			Commands from VampireCommandFramework:
			.help-legacy [search=]
			.help [search=]
			""");
	}

	[Test]
	public void HelpCommand_Help_ListsAssemblyMatch()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help VampireCommandFramework"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Commands from VampireCommandFramework:
			.help-legacy [search=]
			.help [search=]
			""");
	}

	[Test]
	public void HelpCommand_Help_ShowSpecificCommand()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help help-legacy"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] help-legacy (help-legacy) Passes through a .help command that is compatible with other mods that don't use VCF.
			.help-legacy [search=]
			Aliases: .help-legacy
			""");
	}

	[Test]
	public void HelpCommand_Help_ListAll_IncludesNewCommands()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		Assert.That(CommandRegistry.Handle(AnyCtx, ".help"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Listing all commands
			Commands from VampireCommandFramework:
			.help-legacy [search=]
			.help [search=]
			Commands from VCF.Tests:
			.test-help (someEnum) [someType=]
			""");
	}

	[Test]
	public void GenerateHelpText_UsageSpecified()
	{
		var (commandName, usage, description) = Any.ThreeStrings();

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: usage, description: description), null, null, null, null, null, null);
		var text = HelpCommands.PrintShortHelp(command);
		Assert.That(text, Is.EqualTo($".{commandName} {usage}"));
	}

	[Test]
	public void GenerateHelpText_GeneratesUsage_NormalParam()
	{
		var (commandName, usage, description) = Any.ThreeStrings();
		var param = A.Fake<ParameterInfo>();
		var paramName = Any.String();
		A.CallTo(() => param.Name).Returns(paramName);

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: null, description: description), null, null, new[] { param }, null, null, null);

		var text = HelpCommands.PrintShortHelp(command);

		Assert.That(text, Is.EqualTo($".{commandName} ({paramName})"));
	}

	[Test]
	public void GenerateHelpText_GeneratesUsage_DefaultParam()
	{
		var (commandName, usage, description) = Any.ThreeStrings();
		var (paramName, paramValue) = Any.TwoStrings(maxLength: 10);
		var param = A.Fake<ParameterInfo>();

		A.CallTo(() => param.Name).Returns(paramName);
		A.CallTo(() => param.DefaultValue).Returns(paramValue);
		A.CallTo(() => param.HasDefaultValue).Returns(true);

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: null, description: description), null, null, new[] { param }, null, null, null);

		var text = HelpCommands.PrintShortHelp(command);

		Assert.That(text, Is.EqualTo($".{commandName} [{paramName}={paramValue}]"));
	}

	[Test]
	public void FullHelp_Usage_Includes_IConverterUsage()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		var ctx = new AssertReplyContext();
		Format.Mode = Format.FormatMode.None;
		Assert.That(CommandRegistry.Handle(ctx, ".help test-help"), Is.EqualTo(CommandResult.Success));
		ctx.AssertReplyContains("SomeType: TEST-SENTINEL");
	}
	
	[Test]
	public void FullHelp_Usage_Includes_Enum_Values()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		var ctx = new AssertReplyContext();
		Format.Mode = Format.FormatMode.None;
		Assert.That(CommandRegistry.Handle(ctx, ".help test-help"), Is.EqualTo(CommandResult.Success));
		ctx.AssertReplyContains("SomeEnum Values: A, B, C");
	}
}