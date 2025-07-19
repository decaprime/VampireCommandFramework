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
			public void EchoRemainder(ICommandContext ctx, string _remainder)
			{
				ctx.Reply($"Remainder: '{_remainder}'");
			}

			[Command("say", description: "Says something with a prefix")]
			public void SayWithPrefix(ICommandContext ctx, string prefix, string _remainder)
			{
				ctx.Reply($"{prefix}: {_remainder}");
			}

			[Command("optional", description: "Command with optional parameter and remainder")]
			public void OptionalWithRemainder(ICommandContext ctx, string required, int optional = 42, string _remainder = "")
			{
				ctx.Reply($"Required: {required}, Optional: {optional}, Remainder: '{_remainder}'");
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
			public void TestRemainder(ICommandContext ctx, string arg1, string _remainder)
			{
				ctx.Reply($"Remainder test: {arg1}, '{_remainder}'");
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
	}
}