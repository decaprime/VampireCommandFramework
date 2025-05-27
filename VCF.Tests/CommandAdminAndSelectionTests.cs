using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests
{
    public class CommandAdminAndSelectionTests
    {
        [SetUp]
        public void Setup()
        {
            CommandRegistry.Reset();
            Format.Mode = Format.FormatMode.None;
        }

        #region Test Commands

        // Define admin-only commands
        public class AdminCommands
        {
            [Command("admin", adminOnly: true, description: "Admin-only command")]
            public void AdminOnly(ICommandContext ctx)
            {
                ctx.Reply("Admin command executed");
            }

            [Command("admin-int", adminOnly: true, description: "Admin-only int command")]
            public void AdminInt(ICommandContext ctx, int value)
            {
                ctx.Reply($"Admin int command executed with: {value}");
            }
        }

        // Define regular commands with same name but different params
        public class RegularCommands
        {
            [Command("admin", description: "Regular user command")]
            public void RegularAdmin(ICommandContext ctx, string value)
            {
                ctx.Reply($"Regular command executed with: {value}");
            }

            [Command("dual")]
            public void DualCommand(ICommandContext ctx, int value)
            {
                ctx.Reply($"Dual int command executed with: {value}");
            }

            [Command("dual")]
            public void DualCommand(ICommandContext ctx, float value)
            {
                ctx.Reply($"Dual float command executed with: {value}");
            }
        }

        #endregion

        #region Admin Access Tests

        [Test]
        public void AdminCommand_WhenUserIsAdmin_ExecutesCommand()
        {
            // Register admin command
            CommandRegistry.RegisterCommandType(typeof(AdminCommands));

            var ctx = new AssertReplyContext { IsAdmin = true };
            var result = CommandRegistry.Handle(ctx, ".admin");

            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("Admin command executed");
        }

        [Test]
        public void AdminCommand_WhenUserIsNotAdmin_DeniesAccess()
        {
            // Register admin command
            CommandRegistry.RegisterCommandType(typeof(AdminCommands));

            var ctx = new AssertReplyContext { IsAdmin = false };
            var result = CommandRegistry.Handle(ctx, ".admin");

            Assert.That(result, Is.EqualTo(CommandResult.Denied));
            ctx.AssertReplyContains("[denied]");
        }

        [Test]
        public void OverloadedCommands_AdminAndRegular_ExecutesCorrectlyBasedOnAccess()
        {
            // Register both admin and regular commands
            CommandRegistry.RegisterCommandType(typeof(AdminCommands));
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // Test non-admin user - should execute regular command
            var nonAdminCtx = new AssertReplyContext { IsAdmin = false };
            var nonAdminResult = CommandRegistry.Handle(nonAdminCtx, ".admin test");

            Assert.That(nonAdminResult, Is.EqualTo(CommandResult.Success));
            nonAdminCtx.AssertReplyContains("Regular command executed with: test");

            // Test admin user - admin command has no params, so should get ambiguity
            var adminCtx = new AssertReplyContext { IsAdmin = true };
            var adminResult = CommandRegistry.Handle(adminCtx, ".admin");

            Assert.That(adminResult, Is.EqualTo(CommandResult.Success));
            adminCtx.AssertReplyContains("Admin command executed");
        }

        [Test]
        public void OverloadedCommands_AdminAndRegularWithParams_AdminSelectsCorrectly()
        {
            // Register both admin and regular commands
            CommandRegistry.RegisterCommandType(typeof(AdminCommands));
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // Admin user with a parameter that only matches regular command
            var adminCtx = new AssertReplyContext { IsAdmin = true };
            var adminResult = CommandRegistry.Handle(adminCtx, ".admin test");

            Assert.That(adminResult, Is.EqualTo(CommandResult.Success));
            adminCtx.AssertReplyContains("Regular command executed with: test");
        }

        [Test]
        public void OverloadedCommands_AdminInt_AdminCanAccess()
        {
            // Register both admin and regular commands
            CommandRegistry.RegisterCommandType(typeof(AdminCommands));
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // Admin user with int parameter
            var adminCtx = new AssertReplyContext { IsAdmin = true };
            var adminResult = CommandRegistry.Handle(adminCtx, ".admin-int 42");

            Assert.That(adminResult, Is.EqualTo(CommandResult.Success));
            adminCtx.AssertReplyContains("Admin int command executed with: 42");
        }

        [Test]
        public void OverloadedCommands_AdminInt_NonAdminCannotAccess()
        {
            // Register both admin and regular commands
            CommandRegistry.RegisterCommandType(typeof(AdminCommands));
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // Non-admin user trying to access admin-int command
            var nonAdminCtx = new AssertReplyContext { IsAdmin = false };
            var nonAdminResult = CommandRegistry.Handle(nonAdminCtx, ".admin-int 42");

            Assert.That(nonAdminResult, Is.EqualTo(CommandResult.Denied));
            nonAdminCtx.AssertReplyContains("[denied]");
        }

        #endregion

        #region Command Selection Isolation Tests

        [Test]
        public void CommandSelection_DifferentUsers_IsolatedSelections()
        {
            // Register commands that would trigger selection
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // First user gets dual command options
            var user1 = new AssertReplyContext { Name = "User1" };
            var result1 = CommandRegistry.Handle(user1, ".dual 42");

            Assert.That(result1, Is.EqualTo(CommandResult.Success));
            user1.AssertReplyContains("Multiple commands match this input");

            // Second user should not be able to select from first user's options
            var user2 = new AssertReplyContext { Name = "User2" };
            var result2 = CommandRegistry.Handle(user2, ".1");

            Assert.That(result2, Is.Not.EqualTo(CommandResult.Success));
            user2.AssertReplyContains("No command selection is pending");
        }

        [Test]
        public void CommandSelection_SameCommandDifferentUsers_IndependentSelections()
        {
            // Register commands that would trigger selection
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // First user gets dual command options
            var user1 = new AssertReplyContext { Name = "User1" };
            CommandRegistry.Handle(user1, ".dual 42");

            // Second user also gets dual command options
            var user2 = new AssertReplyContext { Name = "User2" };
            CommandRegistry.Handle(user2, ".dual 99");

            // First user selects option 1
            var result1 = CommandRegistry.Handle(user1, ".1");
            Assert.That(result1, Is.EqualTo(CommandResult.Success));
            user1.AssertReplyContains("with: 42"); // Should have the original value

            // Second user selects option 1
            var result2 = CommandRegistry.Handle(user2, ".1");
            Assert.That(result2, Is.EqualTo(CommandResult.Success));
            user2.AssertReplyContains("with: 99"); // Should have their own value
        }

        [Test]
        public void CommandSelection_DisappearsAfterExecution()
        {
            // Register commands that would trigger selection
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // User gets dual command options
            var user = new AssertReplyContext { Name = "User" };
            CommandRegistry.Handle(user, ".dual 42");

            // User selects option 1
            var result1 = CommandRegistry.Handle(user, ".1");
            Assert.That(result1, Is.EqualTo(CommandResult.Success));

            // Selection should be cleared after execution
            var result2 = CommandRegistry.Handle(user, ".1");
            Assert.That(result2, Is.Not.EqualTo(CommandResult.Success));
            user.AssertReplyContains("No command selection is pending");
        }

        [Test]
        public void CommandSelection_InvalidSelection_DoesNotClearOptions()
        {
            // Register commands that would trigger selection
            CommandRegistry.RegisterCommandType(typeof(RegularCommands));

            // User gets dual command options
            var user = new AssertReplyContext { Name = "User" };
            CommandRegistry.Handle(user, ".dual 42");

            // User selects invalid option
            var result1 = CommandRegistry.Handle(user, ".99");
            Assert.That(result1, Is.EqualTo(CommandResult.UsageError));
            user.AssertReplyContains("Invalid selection");

            // Selection should still be available
            var result2 = CommandRegistry.Handle(user, ".1");
            Assert.That(result2, Is.EqualTo(CommandResult.Success));
            user.AssertReplyContains("with: 42");
        }

        #endregion
    }
}
