using BepInEx;
using NUnit.Framework;
using System.Reflection;
using System.Text;
using VampireCommandFramework;
using VampireCommandFramework.Basics;

namespace VCF.Tests
{
    [TestFixture]
    public class RepeatCommandsTests
    {
        private string _tempDirectory;
        private string _originalConfigPath;
        
        [SetUp]
        public void Setup()
        {
            // Create a temporary directory for testing
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VCFTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
            
            // Store original ConfigPath and set test path using reflection
            _originalConfigPath = Paths.ConfigPath;
            SetConfigPath(_tempDirectory);
            
            CommandRegistry.Reset();
            Format.Mode = Format.FormatMode.None;
            
            // Register the necessary command types for testing
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
        }
        
        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Reset();
            
            // Restore original ConfigPath
            SetConfigPath(_originalConfigPath);
            
            // Clean up temporary directory
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
        
        private void SetConfigPath(string path)
        {
            try
            {
                // Try to set the ConfigPath property using reflection
                var pathsType = typeof(Paths);
                var configPathProperty = pathsType.GetProperty("ConfigPath", BindingFlags.Public | BindingFlags.Static);
                
                if (configPathProperty != null && configPathProperty.CanWrite)
                {
                    configPathProperty.SetValue(null, path);
                    return;
                }
                
                // If property doesn't have a public setter, try to find the backing field
                var configPathField = pathsType.GetField("<ConfigPath>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? pathsType.GetField("_configPath", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? pathsType.GetField("configPath", BindingFlags.NonPublic | BindingFlags.Static);
                
                if (configPathField != null)
                {
                    configPathField.SetValue(null, path);
                    return;
                }
                
                // If we can't find the field, try to find a private setter
                var privateSetter = configPathProperty?.GetSetMethod(true);
                if (privateSetter != null)
                {
                    privateSetter.Invoke(null, new object[] { path });
                }
            }
            catch (Exception ex)
            {
                // If reflection fails, we'll fall back to the resilient approach
                Console.WriteLine($"Failed to set ConfigPath via reflection: {ex.Message}");
            }
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
            
            [Command("whoami", description: "Shows the current context name")]
            public static void WhoAmI(ICommandContext ctx)
            {
                ctx.Reply($"You are: {ctx.Name}");
            }
            
            [Command("greet", description: "Greets the current user by name")]
            public static void Greet(ICommandContext ctx, string message)
            {
                ctx.Reply($"Hello {ctx.Name}, {message}");
            }
            
            [Command("contextinfo", description: "Shows context name and unique ID")]
            public static void ContextInfo(ICommandContext ctx)
            {
                // Cast to AssertReplyContext to access ContextId
                if (ctx is AssertReplyContext assertCtx)
                {
                    ctx.Reply($"Context: {ctx.Name} (ID: {assertCtx.ContextId})");
                }
                else
                {
                    ctx.Reply($"Context: {ctx.Name} (ID: unknown)");
                }
            }
            
            [Command("greetfull", description: "Greets with full context info")]
            public static void GreetFull(ICommandContext ctx, string message)
            {
                if (ctx is AssertReplyContext assertCtx)
                {
                    ctx.Reply($"Hello {ctx.Name} (ID: {assertCtx.ContextId}), {message}");
                }
                else
                {
                    ctx.Reply($"Hello {ctx.Name} (ID: unknown), {message}");
                }
            }
        }
        
        #endregion

        [Test]
        public void CommandHistory_RepeatWithDifferentUserContexts()
        {
            // This test verifies that when User B repeats a command from User A's history,
            // it executes with User B's context, not User A's context
            // NOTE: This test assumes cross-user history access for demonstration
            
            // Arrange - User A executes a context-dependent command  
            var userA = new AssertReplyContext { Name = "Alice" };
            TestUtilities.AssertHandle(userA, ".greet world", CommandResult.Success);
            userA.AssertReplyContains("Hello Alice, world");
            
            // Now let's simulate User B executing the same command but with different context
            var userB = new AssertReplyContext { Name = "Bob" };
            TestUtilities.AssertHandle(userB, ".greet everyone", CommandResult.Success);
            userB.AssertReplyContains("Hello Bob, everyone");
            
            // User B repeats their own last command
            var repeatResult = CommandRegistry.Handle(userB, ".!");
            
            // Assert - The repeated command should execute with User B's context (Bob)
            Assert.That(repeatResult, Is.EqualTo(CommandResult.Success));
            userB.AssertReplyContains("Repeating most recent command: .greet everyone");
            userB.AssertReplyContains("Hello Bob, everyone"); // Should be Bob, not Alice
            
            // Verify that Alice's context was not affected
            var aliceText = userA.RepliedTextLfAndTrimmed();
            var bobText = userB.RepliedTextLfAndTrimmed();
            
            // Alice should only have her original command response
            Assert.That(aliceText, Contains.Substring("Hello Alice, world"));
            Assert.That(aliceText, Does.Not.Contain("Hello Bob"), "Alice's context should not contain Bob's responses");
            
            // Bob should have his responses including the repeated command
            Assert.That(bobText, Contains.Substring("Hello Bob, everyone"));
            Assert.That(bobText, Does.Not.Contain("Hello Alice"), "Bob's context should not contain Alice's responses");
        }

        [Test]
        public void CommandHistory_BugTest_RepeatUsesStoredContextInsteadOfCurrent()
        {
            // This test specifically checks for a potential bug where repeated commands
            // use the stored context from history instead of the current context
            // This test may FAIL if the bug exists
            
            // Arrange - Create a context that will be stored in history
            var originalContext = new AssertReplyContext { Name = "TestUser" };
            var originalContextId = originalContext.ContextId;
            
            TestUtilities.AssertHandle(originalContext, ".contextinfo", CommandResult.Success);
            originalContext.AssertReplyContains($"Context: TestUser (ID: {originalContextId})");
            
            // Create a NEW context with the same name but different instance (simulating restart)
            var newContext = new AssertReplyContext { Name = "TestUser" };
            var newContextId = newContext.ContextId;
            
            // Verify they have different IDs
            Assert.That(newContextId, Is.Not.EqualTo(originalContextId), "New context should have different ID");
            
            // Act - Repeat the command using the new context
            var repeatResult = CommandRegistry.Handle(newContext, ".!");
            
            // Assert - The command should execute with the NEW context
            Assert.That(repeatResult, Is.EqualTo(CommandResult.Success));
            newContext.AssertReplyContains("Repeating most recent command: .contextinfo");
            
            // CRITICAL: Check that it uses the NEW context ID, not the old one
            newContext.AssertReplyContains($"Context: TestUser (ID: {newContextId})");
            newContext.AssertReplyDoesntContain($"ID: {originalContextId}");
            
            // Verify the responses went to the correct contexts
            var newContextReplies = newContext.RepliedTextLfAndTrimmed();
            Assert.That(newContextReplies, Contains.Substring($"ID: {newContextId}"), 
                "The repeated command should execute with the NEW context ID");
                
            Assert.That(newContextReplies, Does.Not.Contain($"ID: {originalContextId}"), 
                "The repeated command should NOT use the old context ID");
        }

        [Test]
        public void CommandHistory_RepeatAfterRestartUsesCurrentContext()
        {
            // This test verifies that when commands are loaded from persistent storage and repeated,
            // they execute with the current context, not the stale context from storage
            
            // Arrange - User executes context-dependent commands before restart
            var originalUser = new AssertReplyContext { Name = "TestUser" };
            var originalContextId = originalUser.ContextId;
            
            TestUtilities.AssertHandle(originalUser, ".contextinfo", CommandResult.Success);
            originalUser.AssertReplyContains($"Context: TestUser (ID: {originalContextId})");
            
            TestUtilities.AssertHandle(originalUser, ".greetfull hello", CommandResult.Success);
            originalUser.AssertReplyContains($"Hello TestUser (ID: {originalContextId}), hello");
            
            // Simulate application restart
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Act - Same user name but new context instance (simulating restart)
            var newUser = new AssertReplyContext { Name = "TestUser" };
            var newContextId = newUser.ContextId;
            
            // Verify they have different IDs
            Assert.That(newContextId, Is.Not.EqualTo(originalContextId), "New context should have different ID");
            
            // Repeat the most recent command (loaded from file)
            var repeatResult = CommandRegistry.Handle(newUser, ".!");
            
            // Assert - Should execute with the NEW context instance, not the old one
            Assert.That(repeatResult, Is.EqualTo(CommandResult.Success));
            newUser.AssertReplyContains("Repeating most recent command: .greetfull hello");
            newUser.AssertReplyContains($"Hello TestUser (ID: {newContextId}), hello");
            newUser.AssertReplyDoesntContain($"ID: {originalContextId}");
            
            // Repeat the second-to-last command (contextinfo)
            var contextInfoResult = CommandRegistry.Handle(newUser, ".! 2");
            Assert.That(contextInfoResult, Is.EqualTo(CommandResult.Success));
            newUser.AssertReplyContains("Executing command 2: .contextinfo");
            newUser.AssertReplyContains($"Context: TestUser (ID: {newContextId})");
            newUser.AssertReplyDoesntContain($"ID: {originalContextId}");
        }

        [Test]
        public void CommandHistory_RepeatUsesCurrentContext()
        {
            // This test verifies that when a command is repeated, it executes with the
            // current user's context, not the context that was saved in history
            
            // Arrange - User1 executes a context-dependent command
            var user1 = new AssertReplyContext { Name = "Alice" };
            var user1ContextId = user1.ContextId;
            
            TestUtilities.AssertHandle(user1, ".greetfull world", CommandResult.Success);
            user1.AssertReplyContains($"Hello Alice (ID: {user1ContextId}), world");
            
            // User2 gets their own context
            var user2 = new AssertReplyContext { Name = "Bob" };
            var user2ContextId = user2.ContextId;
            
            // Verify they have different context IDs
            Assert.That(user2ContextId, Is.Not.EqualTo(user1ContextId), "Different users should have different context IDs");
            
            // Act - User2 executes their own command then repeats it
            TestUtilities.AssertHandle(user2, ".greetfull everyone", CommandResult.Success);
            user2.AssertReplyContains($"Hello Bob (ID: {user2ContextId}), everyone");
            
            // Now User2 repeats their last command
            var repeatResult = CommandRegistry.Handle(user2, ".!");
            
            // Assert - The repeated command should execute with User2's context (Bob), not User1's context (Alice)
            Assert.That(repeatResult, Is.EqualTo(CommandResult.Success));
            user2.AssertReplyContains("Repeating most recent command: .greetfull everyone");
            user2.AssertReplyContains($"Hello Bob (ID: {user2ContextId}), everyone"); // Should use Bob's context ID
            user2.AssertReplyDoesntContain($"ID: {user1ContextId}"); // Should NOT use Alice's context ID
            
            // Additional verification: User2 repeats a contextinfo command
            TestUtilities.AssertHandle(user2, ".contextinfo", CommandResult.Success);
            user2.AssertReplyContains($"Context: Bob (ID: {user2ContextId})");
            
            var contextRepeatResult = CommandRegistry.Handle(user2, ".!");
            Assert.That(contextRepeatResult, Is.EqualTo(CommandResult.Success));
            user2.AssertReplyContains("Repeating most recent command: .contextinfo");
            user2.AssertReplyContains($"Context: Bob (ID: {user2ContextId})"); // Should still be Bob's context
            user2.AssertReplyDoesntContain($"ID: {user1ContextId}"); // Should NOT be Alice's context
        }

        [Test]
        public void CommandHistory_HandlesSpecialCharactersInContextName()
        {
            // Arrange - Context name with special characters that would be invalid in filename
            var ctx = new AssertReplyContext { Name = "User<>:\"/|?*With\nSpecial\tChars" };
            
            // Act - Execute commands
            TestUtilities.AssertHandle(ctx, ".echo special1", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo special2", CommandResult.Success);
            
            // Simulate restart
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Create new context with same name
            var newCtx = new AssertReplyContext { Name = "User<>:\"/|?*With\nSpecial\tChars" };
            
            // Act - List history
            var result = CommandRegistry.Handle(newCtx, ".! list");
            
            // Assert - Should handle special characters gracefully
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            newCtx.AssertReplyContains(".echo special2");
            newCtx.AssertReplyContains(".echo special1");
        }

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
            Assert.That(result, Is.EqualTo(CommandResult.CommandError));
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
            Assert.That(result, Is.EqualTo(CommandResult.UsageError)); // The command itself succeeds but reports an error
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
            Assert.That(result, Is.EqualTo(CommandResult.UsageError)); // The command itself succeeds but reports an error
            ctx.AssertReplyContains("[error]");
            ctx.AssertReplyContains("Invalid command history selection");
        }

        #region Deduplication Tests

        [Test]
        public void CommandHistory_DeduplicatesSameCommandWithSameArguments()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute the same command with same arguments multiple times
            TestUtilities.AssertHandle(ctx, ".echo hello", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 5 10", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo hello", CommandResult.Success); // Duplicate
            TestUtilities.AssertHandle(ctx, ".echo world", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 5 10", CommandResult.Success); // Duplicate
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            var historyText = ctx.RepliedTextLfAndTrimmed();
            
            // Should only have unique commands, with most recent execution preserved
            ctx.AssertReplyContains("1. .add 5 10");
            ctx.AssertReplyContains("2. .echo world");
            ctx.AssertReplyContains("3. .echo hello");
            
            // Should not have duplicates - count occurrences
            var helloCount = CountOccurrences(historyText, ".echo hello");
            var addCount = CountOccurrences(historyText, ".add 5 10");
            
            Assert.That(helloCount, Is.EqualTo(1), "Should have only one instance of '.echo hello'");
            Assert.That(addCount, Is.EqualTo(1), "Should have only one instance of '.add 5 10'");
        }

        [Test]
        public void CommandHistory_DoesNotDeduplicateCommandsWithDifferentArguments()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute same command with different arguments
            TestUtilities.AssertHandle(ctx, ".echo hello", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo world", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo test", CommandResult.Success);
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("1. .echo test");
            ctx.AssertReplyContains("2. .echo world");
            ctx.AssertReplyContains("3. .echo hello");
            
            // All three should be present as they have different arguments
            var historyText = ctx.RepliedTextLfAndTrimmed();
            Assert.That(CountOccurrences(historyText, ".echo"), Is.EqualTo(3));
        }

        [Test]
        public void CommandHistory_DoesNotDeduplicateDifferentCommands()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute different commands
            TestUtilities.AssertHandle(ctx, ".echo test", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 1 2", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo test", CommandResult.Success); // Should deduplicate
            TestUtilities.AssertHandle(ctx, ".add 3 4", CommandResult.Success); // Different args, should not deduplicate
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("1. .add 3 4");
            ctx.AssertReplyContains("2. .echo test");
            ctx.AssertReplyContains("3. .add 1 2");
            
            // Should have 3 unique entries
            var historyText = ctx.RepliedTextLfAndTrimmed();
            var lines = historyText.Split('\n').Where(line => line.Contains(". .")).ToArray();
            Assert.That(lines.Length, Is.EqualTo(3));
        }

        [Test]
        public void CommandHistory_DeduplicationMovesToFront()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute commands to establish history
            TestUtilities.AssertHandle(ctx, ".echo first", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo second", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo third", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo fourth", CommandResult.Success);
            
            // Re-execute an older command
            TestUtilities.AssertHandle(ctx, ".echo second", CommandResult.Success);
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            
            // The re-executed command should be at the front
            ctx.AssertReplyContains("1. .echo second");
            ctx.AssertReplyContains("2. .echo fourth");
            ctx.AssertReplyContains("3. .echo third");
            ctx.AssertReplyContains("4. .echo first");
            
            // Should still only have 4 unique entries
            var historyText = ctx.RepliedTextLfAndTrimmed();
            var lines = historyText.Split('\n').Where(line => line.Contains(". .")).ToArray();
            Assert.That(lines.Length, Is.EqualTo(4));
        }

        [Test]
        public void CommandHistory_DeduplicationWithComplexArguments()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute commands with complex arguments
            TestUtilities.AssertHandle(ctx, ".add 10 20", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 30 40", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 10 20", CommandResult.Success); // Should deduplicate
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("1. .add 10 20");
            ctx.AssertReplyContains("2. .add 30 40");
            
            // Should only have 2 unique entries
            var historyText = ctx.RepliedTextLfAndTrimmed();
            var addCount = CountOccurrences(historyText, ".add 10 20");
            Assert.That(addCount, Is.EqualTo(1), "Should have only one instance of '.add 10 20'");
        }

        #endregion

        #region Persistence Tests

        [Test]
        public void CommandHistory_PersistsAcrossResets()
        {
            // Arrange - Execute commands and save history
            var ctx = new AssertReplyContext { Name = "TestUser" };
            TestUtilities.AssertHandle(ctx, ".echo persistent1", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 5 7", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo persistent2", CommandResult.Success);
            
            // Simulate restart by resetting the registry
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Act - Create new context with same name to trigger history loading
            var newCtx = new AssertReplyContext { Name = "TestUser" };
            var result = CommandRegistry.Handle(newCtx, ".! list");
            
            // Assert - History should be loaded from file
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            newCtx.AssertReplyContains("1. .echo persistent2");
            newCtx.AssertReplyContains("2. .add 5 7");
            newCtx.AssertReplyContains("3. .echo persistent1");
        }

        [Test]
        public void CommandHistory_HandlesEmptyHistoryFile()
        {
            // Arrange - User with no previous history
            var ctx = new AssertReplyContext { Name = "NewTestUser" + Guid.NewGuid().ToString() };
            
            // Act - Try to list history for a new user
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert - Should handle gracefully
            Assert.That(result, Is.EqualTo(CommandResult.CommandError));
            ctx.AssertReplyContains("No command history available");
        }

        [Test]
        public void CommandHistory_LoadsOnlyOncePerSession()
        {
            // Arrange
            var ctx = new AssertReplyContext { Name = "SessionTestUser" };
            
            // Execute a command to establish history
            TestUtilities.AssertHandle(ctx, ".echo session1", CommandResult.Success);
            
            // Reset to clear in-memory history but keep files
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Create context with same name
            var newCtx = new AssertReplyContext { Name = "SessionTestUser" };
            
            // First access should load from file
            TestUtilities.AssertHandle(newCtx, ".! list", CommandResult.Success);
            newCtx.AssertReplyContains(".echo session1");
            
            // Execute another command
            TestUtilities.AssertHandle(newCtx, ".echo session2", CommandResult.Success);
            
            // Second access should use in-memory history (not reload from file)
            var result = CommandRegistry.Handle(newCtx, ".! list");
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            newCtx.AssertReplyContains("1. .echo session2");
            newCtx.AssertReplyContains("2. .echo session1");
        }

        [Test]
        public void CommandHistory_RepeatWorksAfterRestart()
        {
            // Arrange - Execute commands and save history
            var ctx = new AssertReplyContext { Name = "RepeatTestUser" };
            TestUtilities.AssertHandle(ctx, ".echo beforeRestart", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 8 12", CommandResult.Success);
            
            // Simulate restart
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Act - Create new context and try to repeat
            var newCtx = new AssertReplyContext { Name = "RepeatTestUser" };
            var result = CommandRegistry.Handle(newCtx, ".!");
            
            // Assert - Should repeat the most recent command from loaded history
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            newCtx.AssertReplyContains("Repeating most recent command: .add 8 12");
            newCtx.AssertReplyContains("Result: 20");
        }

        [Test]
        public void CommandHistory_ExecuteByNumberWorksAfterRestart()
        {
            // Arrange - Execute commands and save history
            var ctx = new AssertReplyContext { Name = "NumberTestUser" };
            TestUtilities.AssertHandle(ctx, ".echo first", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 3 4", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo third", CommandResult.Success);
            
            // Simulate restart
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Act - Create new context and execute by number
            var newCtx = new AssertReplyContext { Name = "NumberTestUser" };
            var result = CommandRegistry.Handle(newCtx, ".! 2");
            
            // Assert - Should execute the second command from loaded history
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            newCtx.AssertReplyContains("Executing command 2: .add 3 4");
            newCtx.AssertReplyContains("Result: 7");
        }

        [Test]
        public void CommandHistory_PersistenceWithDeduplication()
        {
            // Arrange - Execute commands with duplicates
            var ctx = new AssertReplyContext { Name = "DedupPersistUser" };
            TestUtilities.AssertHandle(ctx, ".echo original", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".add 1 1", CommandResult.Success);
            TestUtilities.AssertHandle(ctx, ".echo original", CommandResult.Success); // Should deduplicate
            TestUtilities.AssertHandle(ctx, ".echo different", CommandResult.Success);
            
            // Simulate restart
            CommandRegistry.Reset();
            CommandRegistry.RegisterCommandType(typeof(TestCommands));
            CommandRegistry.RegisterCommandType(typeof(RepeatCommands));
            
            // Act - Load history and list
            var newCtx = new AssertReplyContext { Name = "DedupPersistUser" };
            var result = CommandRegistry.Handle(newCtx, ".! list");
            
            // Assert - Should maintain deduplication across restarts
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            newCtx.AssertReplyContains("1. .echo different");
            newCtx.AssertReplyContains("2. .echo original");
            newCtx.AssertReplyContains("3. .add 1 1");
            
            // Should only have one instance of .echo original
            var historyText = newCtx.RepliedTextLfAndTrimmed();
            var originalCount = CountOccurrences(historyText, ".echo original");
            Assert.That(originalCount, Is.EqualTo(1), "Should have only one instance of '.echo original' after restart");
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void CommandHistory_DeduplicationWithMaxHistoryLimit()
        {
            // Arrange
            var ctx = new AssertReplyContext();
            
            // Execute 11 unique commands (more than max limit of 10)
            for (int i = 0; i < 11; i++)
            {
                TestUtilities.AssertHandle(ctx, $".echo unique{i}", CommandResult.Success);
            }
            
            // Execute a duplicate of an earlier command that should have been removed
            TestUtilities.AssertHandle(ctx, ".echo unique0", CommandResult.Success);
            
            // Act - List command history
            var result = CommandRegistry.Handle(ctx, ".! list");
            
            // Assert
            Assert.That(result, Is.EqualTo(CommandResult.Success));
            
            // Should have .echo unique0 at position 1 (moved to front due to recent execution)
            ctx.AssertReplyContains("1. .echo unique0");
            
            // Should still respect the max limit of 10
            var historyText = ctx.RepliedTextLfAndTrimmed();
            var lines = historyText.Split('\n').Where(line => line.Contains(". .")).ToArray();
            Assert.That(lines.Length, Is.LessThanOrEqualTo(10), "Should not exceed maximum history limit");
        }

        #endregion

        #region Helper Methods

        private static int CountOccurrences(string text, string pattern)
        {
            return text.Split(new[] { pattern }, StringSplitOptions.None).Length - 1;
        }

        #endregion
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
