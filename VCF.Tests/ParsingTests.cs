using Consumer;
using NUnit.Framework;
using FakeItEasy;
using VampireCommandFramework.Registry;
using VampireCommandFramework;

namespace VCF.Tests;

public class ParsingTests
{
	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
	}

	private readonly ICommandContext AnyCtx = A.Fake<ICommandContext>();

	[Test]
	public void CanRegisterConverter()
	{
		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		Assert.Pass();
	}

	[Test]
	public void CanRegisterAssemblyWithCustomConverter()
	{
		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		Assert.Pass();
	}

	[Test]
	public void CanCallParameterless()
	{
		CommandRegistry.RegisterAll(typeof(HorseCommands).Assembly);
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse breed"), Is.EqualTo(CommandResult.Success));
		Assert.Pass();
	}

	[Test]
	public void CanConvertPrimitive()
	{
		CommandRegistry.RegisterAll(typeof(HorseCommands).Assembly);
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse set speed 12.2"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithGroupShorthand()
	{
		CommandRegistry.RegisterAll(typeof(HorseCommands).Assembly);
		Assert.That(CommandRegistry.Handle(AnyCtx, ".h set speed 12.2"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithCustomTypeWithDefault()
	{
		CommandRegistry.RegisterAll();
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse call"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithOverloadedName()
	{
		CommandRegistry.RegisterAll();
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse call 123 41234"), Is.EqualTo(CommandResult.Success));
	}

	[Test]
	public void CanCallWithConverter()
	{
		var ctx = A.Fake<ICommandContext>();
		A.CallTo(() => ctx.Error(A<string>._)).Returns(new CommandException());

		CommandRegistry.RegisterAll();
		Assert.That(CommandRegistry.Handle(ctx, ".horse call Ted"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(ctx, ".horse call Bill"), Is.EqualTo(CommandResult.UsageError));
		A.CallTo(() => ctx.Error("Only Ted")).MustHaveHappenedOnceExactly();
	}

	[Test]
	public void CanCallWithEnum()
	{
		CommandRegistry.RegisterAll();
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color Black"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color Brown"), Is.EqualTo(CommandResult.Success));
		Assert.That(CommandRegistry.Handle(AnyCtx, ".horse color Purple"), Is.EqualTo(CommandResult.UsageError));
	}
}