using FakeItEasy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;

namespace VCF.Tests;

public class CommandContextTests
{
	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		_goodContext = new GoodContext();
		_badContext = new BadContext();
	}

	private static bool DefaultConstructorCalled, ConcreteConstructorCalled, GenericConstructorCalled;
	private GoodContext _goodContext;
	private BadContext _badContext;

	[Test]
	public void Default_Bound_To_Specific_Concrete_Contexts()
	{

		CommandRegistry.RegisterCommandType(typeof(DefaultConstructorCommands));

		Assert.IsNotNull(CommandRegistry.Handle(_goodContext, ".Default-GoodContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNull(CommandRegistry.Handle(_badContext, ".Default-GoodContextTest"), "Command should not be invoked with wrong context");
		Assert.IsTrue(DefaultConstructorCalled);
	}

	[Test]
	public void Default_Concrete_Can_Call_ICommandContext()
	{
		CommandRegistry.RegisterCommandType(typeof(DefaultConstructorCommands));

		Assert.IsNotNull(CommandRegistry.Handle(_goodContext, ".Default-InterfaceContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNotNull(CommandRegistry.Handle(_badContext, ".Default-InterfaceContextTest"), "Command should be invoked with BadContext");
		Assert.IsTrue(DefaultConstructorCalled);
	}

	[Test]
	public void Concrete_Bound_To_Specific_Concrete_Contexts()
	{
		CommandRegistry.RegisterCommandType(typeof(ConcreteConstructorCommands));

		Assert.IsNotNull(CommandRegistry.Handle(_goodContext, ".Concrete-GoodContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNull(CommandRegistry.Handle(_badContext, ".Concrete-GoodContextTest"), "Command should not be invoked with wrong context");
		Assert.IsTrue(ConcreteConstructorCalled);


		// TODO: factor into unique test what about admin check
		_goodContext.IsAdmin = false;
		Assert.IsNull(CommandRegistry.Handle(_goodContext, ".Concrete-GoodContextTest"), "Command should not be invoked with wrong context");
	}

	[Test]
	public void Concrete_Concrete_Can_Call_ICommandContext()
	{
		CommandRegistry.RegisterCommandType(typeof(ConcreteConstructorCommands));

		Assert.IsNotNull(CommandRegistry.Handle(_goodContext, ".Concrete-InterfaceContextTest"), "Command should be invoked with GoodContext");
		// TODO: Expand on this case, this means you've created a command that can respond more generically than the class containing it can be constructed
		Assert.IsNull(CommandRegistry.Handle(_badContext, ".Concrete-InterfaceContextTest"), "Command should be invoked with BadContext");
		Assert.IsTrue(ConcreteConstructorCalled);
	}

	[Test]
	public void Generic_Bound_To_Specific_Concrete_Contexts()
	{
		CommandRegistry.RegisterCommandType(typeof(GenericConstructorCommands));

		Assert.IsNotNull(CommandRegistry.Handle(_goodContext, ".Generic-GoodContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNull(CommandRegistry.Handle(_badContext, ".Generic-GoodContextTest"), "Command should not be invoked with wrong context");
		Assert.IsTrue(GenericConstructorCalled);
	}

	[Test]
	public void Generic_Concrete_Can_Call_ICommandContext()
	{
		CommandRegistry.RegisterCommandType(typeof(GenericConstructorCommands));

		Assert.IsNotNull(CommandRegistry.Handle(_goodContext, ".Generic-InterfaceContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNotNull(CommandRegistry.Handle(_badContext, ".Generic-InterfaceContextTest"), "Command should be invoked with BadContext");
		Assert.IsTrue(GenericConstructorCalled);
	}

	public class DefaultConstructorCommands
	{
		public DefaultConstructorCommands() { DefaultConstructorCalled = true; }

		[ChatCommand("Default-GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[ChatCommand("Default-InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class ConcreteConstructorCommands
	{
		public ConcreteConstructorCommands(GoodContext generic) { ConcreteConstructorCalled = true; }

		[ChatCommand("Concrete-GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[ChatCommand("Concrete-InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class GenericConstructorCommands
	{
		public GenericConstructorCommands(ICommandContext generic) { GenericConstructorCalled = true; }

		[ChatCommand("Generic-GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[ChatCommand("Generic-InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class GoodContext : ICommandContext
	{
		public IServiceProvider Services => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public bool IsAdmin { get; set; } = true;

		public ChatCommandException Error(string LogMessage)
		{
			throw new NotImplementedException();
		}

		public void Reply(string v) => Log.Debug(v);
		
	}

	public class BadContext : ICommandContext
	{
		public IServiceProvider Services => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public bool IsAdmin { get; set; } = true;

		public ChatCommandException Error(string LogMessage)
		{
			throw new NotImplementedException();
		}

		public void Reply(string v) => Log.Debug(v);

	}


}