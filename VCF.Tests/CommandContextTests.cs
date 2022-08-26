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
	}

	[Test]
	public void Bound_To_Specific_Concrete_Contexts()
	{
		var goodContext = new GoodContext();
		var badContext = new BadContext();
		CommandRegistry.RegisterCommandType(typeof(TestCommands));

		Assert.IsNotNull(CommandRegistry.Handle(goodContext, ".GoodContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNull(CommandRegistry.Handle(badContext, ".GoodContextTest"), "Command should not be invoked with wrong context");
	}

	[Test]
	public void Concrete_Can_Call_ICommandContext()
	{
		var goodContext = new GoodContext();
		var badContext = new BadContext();
		CommandRegistry.RegisterCommandType(typeof(TestCommands));

		Assert.IsNotNull(CommandRegistry.Handle(goodContext, ".InterfaceContextTest"), "Command should be invoked with GoodContext");
		Assert.IsNotNull(CommandRegistry.Handle(badContext, ".InterfaceContextTest"), "Command should be invoked with BadContext");
	}

	public class TestCommands
	{
		[ChatCommand("GoodContextTest", adminOnly: true)]
		public void GoodContextTest(GoodContext ctx) { }

		[ChatCommand("InterfaceContextTest", adminOnly: true)]
		public void InterfaceContextTest(ICommandContext ctx) { }
	}

	public class GoodContext : ICommandContext
	{
		public IServiceProvider Services => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public bool IsAdmin => true;

		public ChatCommandException Error(string LogMessage)
		{
			throw new NotImplementedException();
		}

		public void Reply(string v)
		{
			throw new NotImplementedException();
		}
	}

	public class BadContext : ICommandContext
	{
		public IServiceProvider Services => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public bool IsAdmin => true;

		public ChatCommandException Error(string LogMessage)
		{
			throw new NotImplementedException();
		}

		public void Reply(string v)
		{
			throw new NotImplementedException();
		}
	}


}