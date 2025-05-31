using NUnit.Framework;
using System.Reflection;
using System.Collections.Generic;
using VampireCommandFramework;
using VampireCommandFramework.Registry;
using System.Reflection.Emit;

namespace VCF.Tests
{
    public class AssemblyCommandTests
    {
        [SetUp]
        public void Setup()
        {
            CommandRegistry.Reset();
            Format.Mode = Format.FormatMode.None;
        }

        #region Test Command Classes

        // Define commands for our mock "MyMod1" assembly
        public class MyMod1Commands
        {
            [Command("test", description: "MyMod1 test command")]
            public void TestCommand(ICommandContext ctx)
            {
                ctx.Reply("MyMod1 test command executed");
            }

            [Command("echo", description: "MyMod1 echo command")]
            public void EchoCommand(ICommandContext ctx, string message)
            {
                ctx.Reply($"MyMod1 echo: {message}");
            }
        }

        // Define commands for our mock "MyMod2" assembly
        public class MyMod2Commands
        {
            [Command("test", description: "MyMod2 test command")]
            public void TestCommand(ICommandContext ctx)
            {
                ctx.Reply("MyMod2 test command executed");
            }

            [Command("add", description: "MyMod2 add command")]
            public void AddCommand(ICommandContext ctx, int a, int b)
            {
                ctx.Reply($"MyMod2 sum: {a + b}");
            }
        }

		#endregion

		#region Helper Methods

		/// <summary>
		/// Helper method to register commands and associate them with a mock assembly name
		/// </summary>
		private void RegisterCommandsWithMockAssembly(System.Type commandType, string mockAssemblyName)
		{
			// Register the command type normally
			CommandRegistry.RegisterCommandType(commandType);

			// Get the actual assembly
			var realAssembly = commandType.Assembly;

			// Check if commands were registered for the real assembly
			if (CommandRegistry.AssemblyCommandMap.TryGetValue(realAssembly, out var commandCache))
			{
				// Create a dynamic assembly with our mock name
				var asmName = new AssemblyName(mockAssemblyName);
				var mockAssembly = AssemblyBuilder.DefineDynamicAssembly(
					asmName,
					AssemblyBuilderAccess.Run);

				// Create a new command cache for the mock assembly
				var mockCommandCache = new Dictionary<CommandMetadata, List<string>>();

				// Create a new command cache for the real assembly (without the commands we're moving)
				var newRealCommandCache = new Dictionary<CommandMetadata, List<string>>();

				// Sort each command to either the mock or real assembly cache
				foreach (var entry in commandCache)
				{
					if (entry.Key.Method.DeclaringType == commandType)
					{
						// This command belongs to the commandType we're registering
						// Move it to the mock assembly
						var newCommandMetadata = entry.Key with { Assembly = mockAssembly };
						mockCommandCache[newCommandMetadata] = entry.Value;

						// Update CommandCache
						foreach(var cacheEntry in CommandRegistry._cache._newCache.Values)
						{
							foreach(var commandList in cacheEntry.Values)
							{
								for (int i = 0; i < commandList.Count; i++)
								{
									if (commandList[i].Method == entry.Key.Method)
									{
										commandList[i] = newCommandMetadata;
									}
								}
							}
						}
					}
					else
					{
						// This command belongs to a different type
						// Keep it in the real assembly
						newRealCommandCache[entry.Key] = entry.Value;
					}
				}

				// Update the registry with our new caches
				CommandRegistry.AssemblyCommandMap[mockAssembly] = mockCommandCache;
				CommandRegistry.AssemblyCommandMap[realAssembly] = newRealCommandCache;
			}
		}

        #endregion

        #region Tests

        [Test]
        public void AssemblySpecificCommand_RequiresPrefix()
        {
            // Register commands with mock assemblies
            RegisterCommandsWithMockAssembly(typeof(MyMod1Commands), "MyMod1");
            RegisterCommandsWithMockAssembly(typeof(MyMod2Commands), "MyMod2");

            var ctx = new AssertReplyContext();
            
            // Try without prefix - should fail
            var result1 = CommandRegistry.Handle(ctx, "MyMod1 test");
            Assert.That(result1, Is.EqualTo(CommandResult.Unmatched));
            
            // Try with prefix - should succeed
            var result2 = CommandRegistry.Handle(ctx, ".MyMod1 test");
            Assert.That(result2, Is.EqualTo(CommandResult.Success));
            ctx.AssertReplyContains("MyMod1 test command executed");
        }
        
        [Test]
        public void AssemblySpecificCommand_ExecutesCorrectCommand()
        {
            // Register commands with mock assemblies
            RegisterCommandsWithMockAssembly(typeof(MyMod1Commands), "MyMod1");
            RegisterCommandsWithMockAssembly(typeof(MyMod2Commands), "MyMod2");

            // Test MyMod1 command
            var ctx1 = new AssertReplyContext();
            var result1 = CommandRegistry.Handle(ctx1, ".MyMod1 test");
            Assert.That(result1, Is.EqualTo(CommandResult.Success));
            ctx1.AssertReplyContains("MyMod1 test command executed");
            
            // Test MyMod2 command
            var ctx2 = new AssertReplyContext();
            var result2 = CommandRegistry.Handle(ctx2, ".MyMod2 test");
            Assert.That(result2, Is.EqualTo(CommandResult.Success));
            ctx2.AssertReplyContains("MyMod2 test command executed");
		}

		[Test]
		public void AssemblySpecificCommand_ExecutesAssemblyNameCasing()
		{
			// Register commands with mock assemblies
			RegisterCommandsWithMockAssembly(typeof(MyMod1Commands), "MyMod1");
			RegisterCommandsWithMockAssembly(typeof(MyMod2Commands), "MyMod2");

			// Test MyMod1 command
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".mymod1 test");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("MyMod1 test command executed");

			// Test MyMod2 command
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".MYMOD2 test");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("MyMod2 test command executed");
		}

		[Test]
        public void AssemblySpecificCommand_WithParameters()
        {
            // Register commands with mock assemblies
            RegisterCommandsWithMockAssembly(typeof(MyMod1Commands), "MyMod1");
            RegisterCommandsWithMockAssembly(typeof(MyMod2Commands), "MyMod2");

            // Test MyMod1 command with parameter
            var ctx1 = new AssertReplyContext();
            var result1 = CommandRegistry.Handle(ctx1, ".MyMod1 echo \"Hello world\"");
            Assert.That(result1, Is.EqualTo(CommandResult.Success));
            ctx1.AssertReplyContains("MyMod1 echo: Hello world");
            
            // Test MyMod2 command with parameters
            var ctx2 = new AssertReplyContext();
            var result2 = CommandRegistry.Handle(ctx2, ".MyMod2 add 10 20");
            Assert.That(result2, Is.EqualTo(CommandResult.Success));
            ctx2.AssertReplyContains("MyMod2 sum: 30");
        }
        
        [Test]
        public void InvalidAssemblyName_FallsBackToRegularCommand()
        {
            // Register commands with mock assemblies
            RegisterCommandsWithMockAssembly(typeof(MyMod1Commands), "MyMod1");

            var ctx = new AssertReplyContext();
            
            // Use an invalid assembly name - should try to interpret as a regular command
            var result = CommandRegistry.Handle(ctx, ".NonExistentMod test hello");
            
            Assert.That(result, Is.EqualTo(CommandResult.Unmatched));
        }
        
        [Test]
        public void SameCommandNameInDifferentAssemblies_UsesCorrectOne()
        {
            // Register commands with mock assemblies
            RegisterCommandsWithMockAssembly(typeof(MyMod1Commands), "MyMod1");
            RegisterCommandsWithMockAssembly(typeof(MyMod2Commands), "MyMod2");

            // Both assemblies have a 'test' command, but they should be kept separate
            
            // Test MyMod1 test command
            var ctx1 = new AssertReplyContext();
            var result1 = CommandRegistry.Handle(ctx1, ".MyMod1 test");
            Assert.That(result1, Is.EqualTo(CommandResult.Success));
            ctx1.AssertReplyContains("MyMod1 test command executed");
            
            // Test MyMod2 test command
            var ctx2 = new AssertReplyContext();
            var result2 = CommandRegistry.Handle(ctx2, ".MyMod2 test");
            Assert.That(result2, Is.EqualTo(CommandResult.Success));
            ctx2.AssertReplyContains("MyMod2 test command executed");
        }

        #endregion
    }
}
