using Consumer;
using VampireCommandFramework;
using NUnit.Framework;

namespace VCF.Tests
{
	public class ParsingTests
	{
		[SetUp]
		public void Setup()
		{
			CommandRegistry.Reset();
		}

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
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse breed"));
			Assert.Pass();
		}

		[Test]
		public void CanConvertPrimitive()
		{
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse set speed 12.2"));
		}
		
		[Test]
		public void CanCallWithGroupShorthand()
		{
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			Assert.IsNotNull(CommandRegistry.Handle(null, ".h set speed 12.2"));
		}

		[Test]
		public void CanCallWithCustomTypeWithDefault()
		{
			CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse call"));
		}

		[Test]
		public void CanCallWithOverloadedName()
		{
			CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse call 123 41234"));
		}

		[Test]
		public void CanCallWithConverter()
		{
			CommandRegistry.RegisterConverter(typeof(NamedHorseConverter));
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse call Ted"));
			Assert.IsNull(CommandRegistry.Handle(null, ".horse call Bill"));
		}

		[Test]
		public void CanCallWithEnum()
		{
			CommandRegistry.RegisterAssembly(typeof(HorseCommands).Assembly);
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse color Black"));
			Assert.IsNotNull(CommandRegistry.Handle(null, ".horse color Brown"));
			Assert.IsNull(CommandRegistry.Handle(null, ".horse color Purple"));
		}
	}
}