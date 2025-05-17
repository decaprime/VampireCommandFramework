using NUnit.Framework;
using System.Text;
using VampireCommandFramework;
using VampireCommandFramework.Basics;

namespace VCF.Tests
{
    [TestFixture]
    public class RepeatCommandsTests
    {
        [SetUp]
        public void Setup()
        {
            CommandRegistry.Reset();
            Format.Mode = Format.FormatMode.None;
            
            // Register the necessary command types for testing
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
        }

        #region Test Commands
        
        public static class TestCommands
        {
            [Command("echo", description: "Echoes the provided message")]
            public static void Echo(ICommandContext ctx, string message)
            {
                ctx.Reply($"Echo: {message}");
            }

            [Command("add", description: "Adds two numbers")]
            public static void Add(ICommandContext ctx, int a, int b)
            {
                ctx.Reply($"Result: {a + b}");
            }
        }
        
        #endregion

        [Test]
        public void RepeatLastCommand_ExecutesMostRecentCommand()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute an initial command
            TestUtilities.AssertHandle(ctx, ".echo hello", CommandResult.Success);
            ctx.AssertReplyContains("Echo: hello");
            
            // Act - Use the repeat command
            var result = CommandRegistry.Handle(ctx, ".!");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("Repeating most recent command: .echo hello");
            ctx.AssertReplyContains("Echo: hello");
        }

        [Test]
        public void RepeatLastCommand_NoHistory_ReturnsError()
        {
            // Arrange
            var ctx = new AssertReplyContext() { Name = "NewUser" };
            
            // Act - Attempt to repeat with no history
            var result = CommandRegistry.Handle(ctx, ".!");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success)); // The command itself succeeds but reports an error
            ctx.AssertReplyContains("[error]");
            ctx.AssertReplyContains("No command history available");
        }

        [Test]
        public void ListCommandHistory_DisplaysCommandHistory()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute a few commands to build history
            TestUtilities.AssertHandle(ctx, ".echo first", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo second", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 5 10", CommandResult.Success);
            
            // Act - Use the list command history command
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("Command history:");
            ctx.AssertReplyContains("1. .add 5 10");
            ctx.AssertReplyContains("2. .echo second");
            ctx.AssertReplyContains("3. .echo first");
        }

        [Test]
        public void ListCommandHistory_ShortHand_DisplaysCommandHistory()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute a few commands to build history
            TestUtilities.AssertHandle(ctx, ".echo test1", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo test2", CommandResult.Success);
            
            // Act - Use the shorthand command history command
            var result = CommandRegistry.Handle(ctx, ".! l");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("Command history:");
            ctx.AssertReplyContains("1. .echo test2");
            ctx.AssertReplyContains("2. .echo test1");
        }

        [Test]
        public void ExecuteHistoryCommand_ValidIndex_ExecutesCommand()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute different commands to build history
            TestUtilities.AssertHandle(ctx, ".echo \"first message\"", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 10 20", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo \"third message\"", CommandResult.Success);
            
            // Act - Execute the second command in history (index 2)
            var result = CommandRegistry.Handle(ctx, ".! 2");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("Executing command 2: .add 10 20");
            ctx.AssertReplyContains("Result: 30");
        }

        [Test]
        public void ExecuteHistoryCommand_InvalidIndex_ReturnsError()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute a command to build history
            TestUtilities.AssertHandle(ctx, ".echo test", CommandResult.Success);
            
            // Act - Execute with an index that doesn't exist
            var result = CommandRegistry.Handle(ctx, ".! 99");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success)); // The command itself succeeds but reports an error
            ctx.AssertReplyContains("[error]");
            ctx.AssertReplyContains("Invalid command history selection");
        }

        [Test]
        public void CommandHistory_IsolatedBetweenUsers()
        {
            // Arrange
            var user1 = new AssertReplyContext { Name = "User1" };
            var user2 = new AssertReplyContext { Name = "User2" };
            
            // User 1 executes commands
            TestUtilities.AssertHandle(user1, ".echo \"user1 message\"", CommandResult.Success);
            
            // User 2 executes different commands
            TestUtilities.AssertHandle(user2, ".add 5 7", CommandResult.Success);
            
            // Act - Both users try to repeat their last command
            var resultUser1 = CommandRegistry.Handle(user1, ".!");
            var resultUser2 = CommandRegistry.Handle(user2, ".!");
            
            // Assert - Each user should see their own history
            Assert.That(resultUser1, Is.EqualTo(CommandResult.Success));
            user1.AssertReplyContains("Repeating most recent command: .echo \"user1 message\"");
            user1.AssertReplyContains("Echo: user1 message");
            
            Assert.That(resultUser2, Is.EqualTo(CommandResult.Success));
            user2.AssertReplyContains("Repeating most recent command: .add 5 7");
            user2.AssertReplyContains("Result: 12");
        }

        [Test]
        public void CommandHistory_LimitedToMaximumEntries()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute 11 commands (more than the MAX_COMMAND_HISTORY of 10)
            for (int i = 0; i <= 10; i++)
            {
                TestUtilities.AssertHandle(ctx, $".echo message{i}", CommandResult.Success);
            }
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            
            // Should have most recent 10 commands, but not the oldest one (message1)
            ctx.AssertReplyContains("1. .echo message10");
            ctx.AssertReplyContains("10. .echo message1");
            
            // Get the full response to check message0 is not present
            var fullReplyCtx = new AssertReplyContext();
            CommandRegistry.Handle(fullReplyCtx, ".! list");
            Assert.That(fullReplyCtx.RepliedTextLfAndTrimmed().Contains("message0"), Is.False, 
                "The oldest message should have been removed due to history size limit");
        }

        [Test]
        public void InvalidCommandHistoryCommand_ReturnsError()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute a command to ensure we have history
            TestUtilities.AssertHandle(ctx, ".echo test", CommandResult.Success);
            
            // Act - Execute an invalid history command
            var result = CommandRegistry.Handle(ctx, ".! invalid");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success)); // The command itself succeeds but reports an error
            ctx.AssertReplyContains("[error]");
            ctx.AssertReplyContains("Invalid command history selection");
        }
    }

    // Extension method to get the replied text for testing
    public static class AssertReplyContextExtensions
    {
        public static string RepliedTextLfAndTrimmed(this AssertReplyContext ctx)
        {
            // Using reflection to access the private field _sb
            var fieldInfo = typeof(AssertReplyContext).GetField("_sb", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var sb = (StringBuilder)fieldInfo.GetValue(ctx);
            return sb.ToString()
                .Replace("\r\n", "\n")
                .TrimEnd(Environment.NewLine.ToCharArray());
        }
    }
}
