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
		Log.Instance = new BepInEx.Logging.ManualLogSource("Test");
		CommandRegistry.Reset();
		CommandRegistry.RegisterCommandType(typeof(TestCommands));
		UsersCtx = A.Fake<ICommandContext>();
		AdminCtx = A.Fake<ICommandContext>();
		A.CallTo(() => UsersCtx.IsAdmin).Returns(false);
		A.CallTo(() => AdminCtx.IsAdmin).Returns(true);
	}

	[Test] public void User_Denied_AdminOnly() => Assert.IsNull(CommandRegistry.Handle(UsersCtx, ".adminonly"));
	[Test] public void User_Allowed_AllUsers() => Assert.IsNotNull(CommandRegistry.Handle(UsersCtx, ".allusers"));
	[Test] public void User_Allowed_Default() => Assert.IsNotNull(CommandRegistry.Handle(UsersCtx, ".default"));

	[Test] public void Admin_Allowed_AdminOnly() => Assert.IsNotNull(CommandRegistry.Handle(AdminCtx, ".adminonly"));
	[Test] public void Admin_Allowed_AllUsers() => Assert.IsNotNull(CommandRegistry.Handle(AdminCtx, ".allusers"));
	[Test] public void Admin_Allowed_Default() => Assert.IsNotNull(CommandRegistry.Handle(AdminCtx, ".default"));

	[Test] 
	public void Default_Middleware_Can_Be_Removed()
	{
		Assert.IsNull(CommandRegistry.Handle(UsersCtx, ".adminonly"), "By default user should be denied.");
		CommandRegistry.Middlewares.Clear(); // The intention being you'd replace with a more comprehensive system
		Assert.IsNotNull(CommandRegistry.Handle(UsersCtx, ".adminonly"), "After clearing middle user should be allowed.");
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
