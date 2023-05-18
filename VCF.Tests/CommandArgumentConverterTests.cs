using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;
public class CommandArgumentConverterTests
{
	public record SomeType() { readonly string Unique = Any.String(); };

	public static readonly SomeType ReturnedFromGeneric = new(), ReturnedFromSpecific = new(), DefaultValue = new();

	public class GenericContextConverter : CommandArgumentConverter<SomeType>
	{
		public override SomeType Parse(ICommandContext ctx, string input)
		{
			return ReturnedFromGeneric;
		}
	}

	public class SpecificContextConverter : CommandArgumentConverter<SomeType, SecondaryContext>
	{
		public override SomeType Parse(SecondaryContext ctx, string input)
		{
			return ReturnedFromSpecific;
		}
	}

	public class GenericContextTestCommands
	{
		[Command("test")]
		public void TestCommand(ICommandContext ctx, SomeType value) { }

		[Command("test-default")]
		public void TestWithefault(ICommandContext ctx, SomeType value = null) { }
	}

	public class SecondaryContext : ICommandContext
	{
		public IServiceProvider Services => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public bool IsAdmin => true;

		public CommandException Error(string LogMessage)
		{
			throw new NotImplementedException();
		}

		public void Reply(string v)
		{
		}
	}


	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
	}

	[Test]
	public void CanRegister_GenericContext_Converter()
	{
		CommandRegistry.RegisterConverter(typeof(GenericContextConverter));
	}

	[Test]
	public void CanRegister_SpecificContext_Converter()
	{
		CommandRegistry.RegisterConverter(typeof(SpecificContextConverter));
	}

	[Test]
	public void FailRegister_SpecificContext_WithGeneric()
	{
		CommandRegistry.RegisterConverter(typeof(GenericContextConverter));
		Assert.Throws<ArgumentException>(() => CommandRegistry.RegisterConverter(typeof(SpecificContextConverter)), "An item with the same key has already been added.");
	}

	[Test]
	public void FailRegister_GenericContext_WithSpecific()
	{
		CommandRegistry.RegisterConverter(typeof(SpecificContextConverter));
		Assert.Throws<ArgumentException>(() => CommandRegistry.RegisterConverter(typeof(GenericContextConverter)), "An item with the same key has already been added.");
	}

	[Test]
	public void CanConvert_GenericContext()
	{
		CommandRegistry.RegisterConverter(typeof(GenericContextConverter));
		CommandRegistry.RegisterCommandType(typeof(GenericContextTestCommands));

		Assert.That(CommandRegistry.Handle(A.Fake<ICommandContext>(), ".test something"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanConvert_GenericContext_WithDefault()
	{
		CommandRegistry.RegisterConverter(typeof(GenericContextConverter));
		CommandRegistry.RegisterCommandType(typeof(GenericContextTestCommands));
		var ctx = A.Fake<ICommandContext>();

		Assert.That(CommandRegistry.Handle(ctx, ".test-default"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(ctx, ".test-default something"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanConvert_SpecificContext()
	{
		CommandRegistry.RegisterConverter(typeof(SpecificContextConverter));
		CommandRegistry.RegisterCommandType(typeof(GenericContextTestCommands));
		var ctx = new SecondaryContext();

		Assert.That(CommandRegistry.Handle(ctx, ".test something"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanConvert_SpecificContext_FailedWithUnassignable()
	{
		CommandRegistry.RegisterConverter(typeof(SpecificContextConverter));
		CommandRegistry.RegisterCommandType(typeof(GenericContextTestCommands));
		var ctx = new AssertReplyContext();

		Format.Mode = Format.FormatMode.None;		
		Assert.That(CommandRegistry.Handle(ctx, ".test something"), Is.EqualTo(CommandResult.InternalError));
		ctx.AssertInternalError();
	}

	[Test]
	public void CanConvert_SpecificContext_WithDefault()
	{
		CommandRegistry.RegisterConverter(typeof(SpecificContextConverter));
		CommandRegistry.RegisterCommandType(typeof(GenericContextTestCommands));
		var ctx = new SecondaryContext();

		Assert.That(CommandRegistry.Handle(ctx, ".test-default"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(ctx, ".test-default something"), Is.EqualTo(CommandResult.Success));
	}

}
