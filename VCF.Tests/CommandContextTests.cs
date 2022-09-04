using FakeItEasy;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VampireCommandFramework;
using VampireCommandFramework.Common;
using VampireCommandFramework.Registry;

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
	private GoodContext? _goodContext;
	private BadContext? _badContext;

	[Test]
	public void Default_Bound_To_Specific_Concrete_Contexts()
	{

		CommandRegistry.RegisterCommandType(typeof(DefaultConstructorCommands));
		Assert.That(CommandRegistry.Handle(_goodContext, ".Default-GoodContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with GoodContext");
		Assert.That(CommandRegistry.Handle(_badContext, ".Default-GoodContextTest"), Is.EqualTo(CommandResult.InternalError), "Command should not be invoked with wrong context");
		Assert.IsTrue(DefaultConstructorCalled);
	}

	[Test]
	public void Default_Concrete_Can_Call_ICommandContext()
	{
		CommandRegistry.RegisterCommandType(typeof(DefaultConstructorCommands));

		Assert.That(CommandRegistry.Handle(_goodContext, ".Default-InterfaceContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with GoodContext");
		Assert.That(CommandRegistry.Handle(_badContext, ".Default-InterfaceContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with BadContext");
		Assert.IsTrue(DefaultConstructorCalled);
	}

	[Test]
	public void Concrete_Bound_To_Specific_Concrete_Contexts()
	{
		CommandRegistry.RegisterCommandType(typeof(ConcreteConstructorCommands));

		Assert.That(CommandRegistry.Handle(_goodContext, ".Concrete-GoodContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with GoodContext");
		Assert.That(CommandRegistry.Handle(_badContext, ".Concrete-GoodContextTest"), Is.EqualTo(CommandResult.InternalError), "Command should not be invoked with wrong context");
		Assert.IsTrue(ConcreteConstructorCalled);


		// TODO: factor into unique test what about admin check
		_goodContext.IsAdmin = false;
		Assert.That(CommandRegistry.Handle(_goodContext, ".Concrete-GoodContextTest"), Is.EqualTo(CommandResult.Denied), "Command should not be invoked with wrong context");
	}

	[Test]
	public void Concrete_Concrete_Can_Call_ICommandContext()
	{
		CommandRegistry.RegisterCommandType(typeof(ConcreteConstructorCommands));

		Assert.That(CommandRegistry.Handle(_goodContext, ".Concrete-InterfaceContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with GoodContext");
		// TODO: Expand on this case, this means you've created a command that can respond more generically than the class containing it can be constructed
		Assert.That(CommandRegistry.Handle(_badContext, ".Concrete-InterfaceContextTest"), Is.EqualTo(CommandResult.InternalError), "Command should be invoked with BadContext");
		Assert.IsTrue(ConcreteConstructorCalled);
	}

	[Test]
	public void Generic_Bound_To_Specific_Concrete_Contexts()
	{
		CommandRegistry.RegisterCommandType(typeof(GenericConstructorCommands));

		Assert.That(CommandRegistry.Handle(_goodContext, ".Generic-GoodContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with GoodContext");
		Assert.That(CommandRegistry.Handle(_badContext, ".Generic-GoodContextTest"), Is.EqualTo(CommandResult.InternalError), "Command should not be invoked with wrong context");
		Assert.IsTrue(GenericConstructorCalled);
	}

	[Test]
	public void Generic_Concrete_Can_Call_ICommandContext()
	{
		CommandRegistry.RegisterCommandType(typeof(GenericConstructorCommands));

		Assert.That(CommandRegistry.Handle(_goodContext, ".Generic-InterfaceContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with GoodContext");
		Assert.That(CommandRegistry.Handle(_badContext, ".Generic-InterfaceContextTest"), Is.EqualTo(CommandResult.Success), "Command should be invoked with BadContext");
		Assert.IsTrue(GenericConstructorCalled);
	}

	public class DefaultConstructorCommands
	{
		public DefaultConstructorCommands() { DefaultConstructorCalled = true; }

		[Command("Default-GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[Command("Default-InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class ConcreteConstructorCommands
	{
		public ConcreteConstructorCommands(GoodContext generic) { ConcreteConstructorCalled = true; }

		[Command("Concrete-GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[Command("Concrete-InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class GenericConstructorCommands
	{
		public GenericConstructorCommands(ICommandContext generic) { GenericConstructorCalled = true; }

		[Command("Generic-GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[Command("Generic-InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class GoodContext : ICommandContext
	{
		public IServiceProvider Services => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public bool IsAdmin { get; set; } = true;

		public CommandException Error(string LogMessage)
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

		public CommandException Error(string LogMessage)
		{
			throw new NotImplementedException();
		}

		public void Reply(string v) => Log.Debug(v);

	}


}