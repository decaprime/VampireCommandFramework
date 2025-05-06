using Consumer;
using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests;

public class ParsingTests
{
	public class NamedHorseConverter : CommandArgumentConverter<NamedHorse?>
	{
		// Only Ted apparently
		public override NamedHorse? Parse(ICommandContext ctx, string input)
		{
			/* check some cache or perform entity query, null here to not pull in more */
			return (input == "Ted") ? null : throw ctx.Error("Only Ted");
		}
	}
	
	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		CommandRegistry.RegisterCommandType(typeof(HorseCommands));
	}

	private readonly ICommandContext AnyCtx = A.Fake<ICommandContext>();

	[Test]
	public void CanCallParameterless()
	{
		
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse breed"), Is.EqualTo(CommandResult.Success));
		Assert.Pass();
	}

	[Test]
	public void CanConvertPrimitive()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse set speed 12.2"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithGroupShorthand()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".h set speed 12.2"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithCustomTypeWithDefault()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse call"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithOverloadedName()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse call 123 41234"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithConverter()
	{
		var ctx = A.Fake<ICommandContext>();
		A.CallTo(() => ctx.Error(A<string>._)).Returns(new CommandException());

		Assert.That(CommandRegistry.Handle(ctx, ".horse call Ted"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(ctx, ".horse call Bill"), Is.EqualTo(CommandResult.UsageError));
		A.CallTo(() => ctx.Error("Only Ted")).MustHaveHappenedOnceExactly();
	}

	[Test]
	public void CanCallWithEnum()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color Black"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color Brown"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color Purple"), Is.EqualTo(CommandResult.UsageError));
	}
	
	[Test]
	public void CanCallWithEnumValues()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color 1"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color 2"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color 4"), Is.EqualTo(CommandResult.UsageError));
	}

	[Test]
	public void CommandsCanBeSubstrings()
	{
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse call 1 2"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse caller 1"), Is.EqualTo(CommandResult.Success));
	}
}