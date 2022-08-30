using FakeItEasy;
using NUnit.Framework;
using VampireCommandFramework;
namespace VCF.Tests;

public class BasicAdminMiddlewareTests
{
	private ICommandContext UsersCtx, AdminCtx;

	[SetUp]
	public void Setup()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterCommandType(typeof(TestCommands));
		UsersCtx = A.Fake<ICommandContext>();
		AdminCtx = A.Fake<ICommandContext>();
		A.CallTo(() => UsersCtx.IsAdmin).Returns(false);
		A.CallTo(() => AdminCtx.IsAdmin).Returns(true);
	}

	[Test] public void User_Denied_AdminOnly() => Assert.That(CommandRegistry.Handle(UsersCtx, ".adminonly"), Is.EqualTo(CommandResult.Denied));
	[Test] public void User_Allowed_AllUsers() => Assert.That(CommandRegistry.Handle(UsersCtx, ".allusers"), Is.EqualTo(CommandResult.Success));
	[Test] public void User_Allowed_Default() => Assert.That(CommandRegistry.Handle(UsersCtx, ".default"), Is.EqualTo(CommandResult.Success));

	[Test] public void Admin_Allowed_AdminOnly() => Assert.That(CommandRegistry.Handle(AdminCtx, ".adminonly"), Is.EqualTo(CommandResult.Success));
	[Test] public void Admin_Allowed_AllUsers() => Assert.That(CommandRegistry.Handle(AdminCtx, ".allusers"), Is.EqualTo(CommandResult.Success));
	[Test] public void Admin_Allowed_Default() => Assert.That(CommandRegistry.Handle(AdminCtx, ".default"), Is.EqualTo(CommandResult.Success));

	[Test] 
	public void Default_Middleware_Can_Be_Removed()
	{
		Assert.That(CommandRegistry.Handle(UsersCtx, ".adminonly"), Is.EqualTo(CommandResult.Denied), "By default user should be denied.");
		CommandRegistry.Middlewares.Clear(); // The intention being you'd replace with a more comprehensive system
		Assert.That(CommandRegistry.Handle(UsersCtx, ".adminonly"), Is.EqualTo(CommandResult.Success), "After clearing middle user should be allowed.");
	}

	public class TestCommands
	{
		[ChatCommand("adminonly", adminOnly: true)]
		public void AdminOnly(ICommandContext ctx) { }

		[ChatCommand("allusers", adminOnly: false)]
		public void AlLUsers(ICommandContext ctx) { }

		[ChatCommand("default")]
		public void DefaultCommand(ICommandContext ctx) { }
	}
}
