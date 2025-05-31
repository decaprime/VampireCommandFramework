using FakeItEasy;
using NUnit.Framework;
using System.Reflection;
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


		[Command("searchForCommand")]
		public void TestSearchForCommand(ICommandContext ctx)
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
	public void HelpCommand_Help_ListsAssemblies()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Listing all plugins
			Use .help <plugin> for commands in that plugin
			VampireCommandFramework
			""");
	}

	[Test]
	public void HelpCommand_Help_ListsAssemblyMatch()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help VampireCommandFramework"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Commands from VampireCommandFramework:
			.help [search=] [filter=]
			.help-all [filter=]
			.help-legacy [search=]
			""");
	}

	[Test]
	public void HelpCommand_Help_ShowSpecificCommand()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".help help-legacy"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] help-legacy Passes through a .help command that is compatible with other mods that don't use VCF.
			.help-legacy [search=]
			Aliases: .help-legacy
			""");
	}

	[Test]
	public void HelpCommand_Help_ListAll_IncludesNewCommands()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		Assert.That(CommandRegistry.Handle(AnyCtx, ".help-all"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Listing all commands
			Commands from VampireCommandFramework:
			.help [search=] [filter=]
			.help-all [filter=]
			.help-legacy [search=]
			Commands from VCF.Tests:
			.searchForCommand
			.test-help (someEnum) [someType=]
			""");
	}

	[Test]
	public void HelpCommand_Help_ListAll_Filtered()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		Assert.That(CommandRegistry.Handle(AnyCtx, ".help-all help"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Listing all commands matching filter 'help'
			Commands from VampireCommandFramework:
			.help [search=] [filter=]
			.help-all [filter=]
			.help-legacy [search=]
			Commands from VCF.Tests:
			.test-help (someEnum) [someType=]
			""");
	}

	[Test]
	public void HelpCommand_Help_ListAll_FilteredNoMatch()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		Assert.That(CommandRegistry.Handle(AnyCtx, ".help-all trying"), Is.EqualTo(CommandResult.CommandError));
	}

	[Test]
	public void HelpCommand_Help_ListAssemblies_IncludesNewCommands()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		Assert.That(CommandRegistry.Handle(AnyCtx, ".help"), Is.EqualTo(CommandResult.Success));
		AnyCtx.AssertReply($"""
			[vcf] Listing all plugins
			Use .help <plugin> for commands in that plugin
			VampireCommandFramework
			VCF.Tests
			""");
	}

	[Test]
	public void GenerateHelpText_UsageSpecified()
	{
		var (commandName, usage, description) = Any.ThreeStrings();

		var command = new CommandMetadata(new CommandAttribute(commandName, null, usage: usage, description: description), null, null, null, null, null, null, null);
		var text = HelpCommands.GetShortHelp(command);
		Assert.That(text, Is.EqualTo($".{commandName} {usage}"));
	}

	[Test]
	public void GenerateHelpText_GeneratesUsage_NormalParam()
	{
		var (commandName, usage, description) = Any.ThreeStrings();
		var param = A.Fake<ParameterInfo>();
		var paramName = Any.String();
		A.CallTo(() => param.Name).Returns(paramName);

		var command = new CommandMetadata(new CommandAttribute(commandName, null, usage: null, description: description), null, null, null, new[] { param }, null, null, null);

		var text = HelpCommands.GetShortHelp(command);

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

		var command = new CommandMetadata(new CommandAttribute(commandName, usage: null, description: description), null, null, null, new[] { param }, null, null, null);

		var text = HelpCommands.GetShortHelp(command);

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

	[Test]

	public void SpecifiedAssemblyAndSearchedForCommand()
	{
		CommandRegistry.RegisterConverter(typeof(SomeTypeConverter));
		CommandRegistry.RegisterCommandType(typeof(HelpTestCommands));

		var ctx = new AssertReplyContext();
		Format.Mode = Format.FormatMode.None;
		Assert.That(CommandRegistry.Handle(ctx, ".help vcf.tests seaRCH"), Is.EqualTo(CommandResult.Success));
		ctx.AssertReplyContains("searchForCommand");
		ctx.AssertReplyDoesntContain("test-help");
	}
}