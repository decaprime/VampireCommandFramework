using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests
{
	public class RemainderParameterTests
	{
		[SetUp]
		public void Setup()
		{
			CommandRegistry.Reset();
			Format.Mode = Format.FormatMode.None;
		}

		#region Test Commands

		public class RemainderCommands
		{
			[Command("echo", description: "Echoes the remainder text")]
			public void EchoRemainder(ICommandContext ctx, [Remainder] string message)
			{
				ctx.Reply($"Remainder: '{message}'");
			}

			[Command("say", description: "Says something with a prefix")]
			public void SayWithPrefix(ICommandContext ctx, string prefix, [Remainder] string body)
			{
				ctx.Reply($"{prefix}: {body}");
			}

			[Command("optional", description: "Command with optional parameter and remainder")]
			public void OptionalWithRemainder(ICommandContext ctx, string required, int optional = 42, [Remainder] string reason = "")
			{
				ctx.Reply($"Required: {required}, Optional: {optional}, Remainder: '{reason}'");
			}
		}

		public class ConflictingCommands
		{
			[Command("test", description: "Regular test with fixed params")]
			public void TestRegular(ICommandContext ctx, string arg1, string arg2)
			{
				ctx.Reply($"Regular test: {arg1}, {arg2}");
			}

			[Command("test", description: "Test with remainder")]
			public void TestRemainder(ICommandContext ctx, string arg1, [Remainder] string rest)
			{
				ctx.Reply($"Remainder test: {arg1}, '{rest}'");
			}
		}

		[CommandGroup("grp")]
		public class GroupedRemainderCommands
		{
			[Command("go", description: "Go with remainder")]
			public void Go(ICommandContext ctx, [Remainder] string message)
			{
				ctx.Reply($"Grp go: '{message}'");
			}

			[Command("tag", description: "Tag with prefix and remainder")]
			public void Tag(ICommandContext ctx, string name, [Remainder] string description)
			{
				ctx.Reply($"Grp tag: {name}, '{description}'");
			}
		}

		[CommandGroup("msg", shortHand: "m")]
		public class ShorthandGroupedRemainderCommands
		{
			[Command("send", description: "Send with remainder")]
			public void Send(ICommandContext ctx, [Remainder] string message)
			{
				ctx.Reply($"Message: '{message}'");
			}
		}

		public class MultiWordRemainderCommands
		{
			[Command("fancy thing", description: "Multi-word command with remainder")]
			public void FancyThing(ICommandContext ctx, [Remainder] string text)
			{
				ctx.Reply($"Fancy thing: '{text}'");
			}
		}

		[CommandGroup("stuff")]
		public class GroupedMultiWordRemainderCommands
		{
			[Command("do thing", description: "Grouped multi-word command with remainder")]
			public void DoThing(ICommandContext ctx, [Remainder] string filter)
			{
				ctx.Reply($"Stuff do thing: '{filter}'");
			}
		}

		public class InvalidRemainderPositionCommands
		{
			[Command("badremainder", description: "Has [Remainder] on a non-last parameter — should be rejected at registration")]
			public void BadRemainder(ICommandContext ctx, [Remainder] string text, int trailing)
			{
				ctx.Reply($"{text} {trailing}");
			}
		}

		public class DifferentWordCountShorthandCommands
		{
			[Command("long command", shortHand: "lc", description: "Different word count shorthand")]
			public void LongCommand(ICommandContext ctx, [Remainder] string args)
			{
				ctx.Reply($"Long command: '{args}'");
			}
		}

		[CommandGroup("my group", shortHand: "mg")]
		public class DifferentWordCountGroupShorthandCommands
		{
			[Command("run", description: "Different word count group shorthand")]
			public void Run(ICommandContext ctx, [Remainder] string reason)
			{
				ctx.Reply($"Run: '{reason}'");
			}
		}

		#endregion

		[Test]
		public void RemainderParameter_BasicCapture_HandlesAllCases()
		{
			CommandRegistry.RegisterCommandType(typeof(RemainderCommands));

			// Test single word
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".echo hello");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Remainder: 'hello'");

			// Test multiple words with special characters
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".echo hello world \"quoted text\" !@#$");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Remainder: 'hello world \"quoted text\" !@#$'");

			// Test empty remainder
			var ctx3 = new AssertReplyContext();
			var result3 = CommandRegistry.Handle(ctx3, ".echo");
			Assert.That(result3, Is.EqualTo(CommandResult.Success));
			ctx3.AssertReplyContains("Remainder: ''");
		}

		[Test]
		public void RemainderWithRequiredParameters_Works()
		{
			CommandRegistry.RegisterCommandType(typeof(RemainderCommands));

			// Test with required parameter
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".say INFO this is the remainder message");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("INFO: this is the remainder message");

			// Test missing required parameter returns error
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".say");
			Assert.That(result2, Is.EqualTo(CommandResult.UsageError));
		}

		[Test]
		public void RemainderWithOptionalParameters_HandlesAllScenarios()
		{
			CommandRegistry.RegisterCommandType(typeof(RemainderCommands));

			// All parameters provided
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".optional test 99 this is the remainder");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Required: test, Optional: 99, Remainder: 'this is the remainder'");

			// Optional parameter omitted
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".optional test this is the remainder");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Required: test, Optional: 42, Remainder: 'this is the remainder'");

			// Both optional and remainder omitted
			var ctx3 = new AssertReplyContext();
			var result3 = CommandRegistry.Handle(ctx3, ".optional test");
			Assert.That(result3, Is.EqualTo(CommandResult.Success));
			ctx3.AssertReplyContains("Required: test, Optional: 42, Remainder: ''");
		}

		[Test]
		public void RemainderCommand_OverloadingResolution_SelectsCorrectCommand()
		{
			CommandRegistry.RegisterCommandType(typeof(ConflictingCommands));

			// If it's ambiguous, should prompt for selection
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".test arg1 arg2");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Multiple commands match this input");

			// More args than regular command should use remainder
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".test arg1 this is more than two arguments");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Remainder test: arg1, 'this is more than two arguments'");

			// Fewer args than regular command should use remainder
			var ctx3 = new AssertReplyContext();
			var result3 = CommandRegistry.Handle(ctx3, ".test arg1");
			Assert.That(result3, Is.EqualTo(CommandResult.Success));
			ctx3.AssertReplyContains("Remainder test: arg1, ''");
		}

		[Test]
		public void GroupedCommand_RemainderParameter_ExtractsCorrectly()
		{
			CommandRegistry.RegisterCommandType(typeof(GroupedRemainderCommands));

			// Multiple words as remainder
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".grp go hello world");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Grp go: 'hello world'");

			// Empty remainder
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".grp go");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Grp go: ''");

			// Several words as remainder
			var ctx3 = new AssertReplyContext();
			var result3 = CommandRegistry.Handle(ctx3, ".grp go one two three");
			Assert.That(result3, Is.EqualTo(CommandResult.Success));
			ctx3.AssertReplyContains("Grp go: 'one two three'");
		}

		[Test]
		public void GroupedCommand_RemainderWithRequiredParam_Works()
		{
			CommandRegistry.RegisterCommandType(typeof(GroupedRemainderCommands));

			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".grp tag myname this is the description");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Grp tag: myname, 'this is the description'");
		}

		[Test]
		public void ShorthandGroupedCommand_RemainderParameter_Works()
		{
			CommandRegistry.RegisterCommandType(typeof(ShorthandGroupedRemainderCommands));

			// Full group name
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".msg send hello world");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Message: 'hello world'");

			// Shorthand group name
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".m send hello world");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Message: 'hello world'");
		}

		[Test]
		public void MultiWordCommand_RemainderParameter_ExtractsCorrectly()
		{
			CommandRegistry.RegisterCommandType(typeof(MultiWordRemainderCommands));

			// Multi-word command with remainder
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".fancy thing some extra text");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Fancy thing: 'some extra text'");

			// Multi-word command with empty remainder
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".fancy thing");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Fancy thing: ''");
		}

		[Test]
		public void GroupedMultiWordCommand_RemainderParameter_ExtractsCorrectly()
		{
			CommandRegistry.RegisterCommandType(typeof(GroupedMultiWordRemainderCommands));

			// Grouped + multi-word command with remainder
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".stuff do thing some filter text");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Stuff do thing: 'some filter text'");

			// Grouped + multi-word command with empty remainder
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".stuff do thing");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Stuff do thing: ''");
		}

		[Test]
		public void DifferentWordCountShorthand_RemainderParameter_ExtractsCorrectly()
		{
			CommandRegistry.RegisterCommandType(typeof(DifferentWordCountShorthandCommands));

			// Full name (2 words to skip)
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".long command top 10 players");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Long command: 'top 10 players'");

			// Shorthand (1 word to skip)
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".lc top 10 players");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Long command: 'top 10 players'");
		}

		[Test]
		public void DifferentWordCountGroupShorthand_RemainderParameter_ExtractsCorrectly()
		{
			CommandRegistry.RegisterCommandType(typeof(DifferentWordCountGroupShorthandCommands));

			// Full group name (2+1=3 words to skip)
			var ctx1 = new AssertReplyContext();
			var result1 = CommandRegistry.Handle(ctx1, ".my group run some reason here");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx1.AssertReplyContains("Run: 'some reason here'");

			// Shorthand group name (1+1=2 words to skip)
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".mg run some reason here");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("Run: 'some reason here'");
		}

		[Test]
		public void Remainder_OnNonLastParameter_IsRejected()
		{
			CommandRegistry.RegisterCommandType(typeof(InvalidRemainderPositionCommands));

			// Registration should refuse to install the command (it logs an error and returns).
			// Invoking it should therefore fall through as an unknown command.
			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".badremainder hello 3");
			Assert.That(result, Is.Not.EqualTo(CommandResult.Success));
		}
	}
}