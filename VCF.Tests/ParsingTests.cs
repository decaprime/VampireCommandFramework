using Consumer;
using VampireCommandFramework;
using NUnit.Framework;
using FakeItEasy;

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
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.Pass();
	}

	[Test]
	public void CanCallParameterless()
	{
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".horse breed"));
		Assert.Pass();
	}

	[Test]
	public void CanConvertPrimitive()
	{
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".horse set speed 12.2"));
	}

	[Test]
	public void CanCallWithGroupShorthand()
	{
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".h set speed 12.2"));
	}

	[Test]
	public void CanCallWithCustomTypeWithDefault()
	{
		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".horse call"));
	}

	[Test]
	public void CanCallWithOverloadedName()
	{
		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".horse call 123 41234"));
	}

	[Test]
	public void CanCallWithConverter()
	{
		var ctx = A.Fake<ICommandContext>();
		A.CallTo(() => ctx.Error(A<string>._)).Returns(new ChatCommandException());

		CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(ctx, ".horse call Ted"));
		Assert.IsNull(CommandRegistry.Handle(ctx, ".horse call Bill"));
		A.CallTo(() => ctx.Error("Only Ted")).MustHaveHappenedOnceExactly();
	}

	[Test]
	public void CanCallWithEnum()
	{
		CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".horse color Black"));
		Assert.IsNotNull(CommandRegistry.Handle(AnyCtx, ".horse color Brown"));
		Assert.IsNull(CommandRegistry.Handle(AnyCtx, ".horse color Purple"));
	}
}