using NUnit.Framework;
using VampireCommandFramework;
using VampireCommandFramework.Registry;

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

		// Assembly + remainder-only command
		public class MyMod1RemainderCommands
		{
			[Command("echo", description: "MyMod1 echo with remainder")]
			public void EchoRemainder(ICommandContext ctx, string _remainder)
			{
				ctx.Reply($"MyMod1 remainder: '{_remainder}'");
			}
		}

		// Assembly + param + remainder command
		public class MyMod1ParamRemainderCommands
		{
			[Command("say", description: "MyMod1 say with prefix and remainder")]
			public void SayWithRemainder(ICommandContext ctx, string prefix, string _remainder)
			{
				ctx.Reply($"MyMod1 {prefix}: {_remainder}");
			}
		}

		// Assembly + grouped command + remainder
		[CommandGroup("grp")]
		public class MyMod1GroupedRemainderCommands
		{
			[Command("go", description: "MyMod1 grouped go with remainder")]
			public void Go(ICommandContext ctx, string _remainder)
			{
				ctx.Reply($"MyMod1 grp go: '{_remainder}'");
			}
		}

		// Assembly whose name collides with a command group name
		public class GrpAssemblyCommands
		{
			[Command("unrelated", description: "Unrelated command in grp assembly")]
			public void Unrelated(ICommandContext ctx)
			{
				ctx.Reply("grp assembly unrelated");
			}
		}

		// Command group whose name collides with an assembly name
		[CommandGroup("grp")]
		public class GrpGroupCommands
		{
			[Command("go", description: "Go command in grp group")]
			public void Go(ICommandContext ctx, string _remainder)
			{
				ctx.Reply($"grp group go: '{_remainder}'");
			}
		}

		// Assembly + grouped command + shorthand group + remainder
		[CommandGroup("msg", shortHand: "m")]
		public class MyMod1ShorthandGroupRemainderCommands
		{
			[Command("send", description: "MyMod1 send with remainder")]
			public void Send(ICommandContext ctx, string _remainder)
			{
				ctx.Reply($"MyMod1 msg send: '{_remainder}'");
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

			// Get the actual assembly name
			var realAssemblyName = commandType.Assembly.GetName().Name;

			// Check if commands were registered for the real assembly
			if (CommandRegistry.AssemblyCommandMap.TryGetValue(realAssemblyName, out var commandCache))
			{
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
						var newCommandMetadata = entry.Key with { AssemblyName = mockAssemblyName };
						mockCommandCache[newCommandMetadata] = entry.Value;

						// Update CommandCache (_newCache)
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

						// Update CommandCache (_remainderCache)
						foreach(var remainderList in CommandRegistry._cache._remainderCache.Values)
						{
							for (int i = 0; i < remainderList.Count; i++)
							{
								if (remainderList[i].Method == entry.Key.Method)
								{
									remainderList[i] = newCommandMetadata;
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
				CommandRegistry.AssemblyCommandMap[mockAssemblyName] = mockCommandCache;
				CommandRegistry.AssemblyCommandMap[realAssemblyName] = newRealCommandCache;
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

		[Test]
		public void AssemblySpecificCommand_WithRemainder_ExtractsCorrectly()
		{
			RegisterCommandsWithMockAssembly(typeof(MyMod1RemainderCommands), "MyMod1");

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".MyMod1 echo hello world this is text");
			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("MyMod1 remainder: 'hello world this is text'");
		}

		[Test]
		public void AssemblySpecificCommand_WithParamAndRemainder_ExtractsCorrectly()
		{
			RegisterCommandsWithMockAssembly(typeof(MyMod1ParamRemainderCommands), "MyMod1");

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".MyMod1 say INFO this is the message");
			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("MyMod1 INFO: this is the message");
		}

		[Test]
		public void AssemblySpecificCommand_GroupedWithRemainder_ExtractsCorrectly()
		{
			RegisterCommandsWithMockAssembly(typeof(MyMod1GroupedRemainderCommands), "MyMod1");

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".MyMod1 grp go hello world");
			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("MyMod1 grp go: 'hello world'");
		}

		[Test]
		public void AssemblySpecificCommand_GroupedWithRemainder_EmptyRemainder()
		{
			RegisterCommandsWithMockAssembly(typeof(MyMod1GroupedRemainderCommands), "MyMod1");

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".MyMod1 grp go");
			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("MyMod1 grp go: ''");
		}

		[Test]
		public void AssemblySpecificCommand_ShorthandGroupWithRemainder_FullAndShort()
		{
			RegisterCommandsWithMockAssembly(typeof(MyMod1ShorthandGroupRemainderCommands), "MyMod1");

			// Full group name
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".MyMod1 msg send hello world");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("MyMod1 msg send: 'hello world'");

			// Shorthand group name
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".MyMod1 m send hello world");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("MyMod1 msg send: 'hello world'");
		}

		[Test]
		public void AssemblySpecificCommand_WithRemainder_EmptyRemainder()
		{
			RegisterCommandsWithMockAssembly(typeof(MyMod1RemainderCommands), "MyMod1");

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".MyMod1 echo");
			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("MyMod1 remainder: ''");
		}

		[Test]
		public void AssemblyNameMatchesGroupName_FallsBackToGroupCommand()
		{
			// Register an assembly named "grp" that has no "go" command
			RegisterCommandsWithMockAssembly(typeof(GrpAssemblyCommands), "grp");
			// Register a command group named "grp" with a "go" command (in a different assembly)
			CommandRegistry.RegisterCommandType(typeof(GrpGroupCommands));

			// ".grp go hello" — assembly "grp" exists but has no "go" command,
			// so it should fall back to global lookup and match the "grp" command group's "go" command
			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".grp go hello world");
			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("grp group go: 'hello world'");
		}

        #endregion
    }
}
