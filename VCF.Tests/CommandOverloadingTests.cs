using NUnit.Framework;
using VampireCommandFramework;

namespace VCF.Tests
{
	public class CommandOverloadingTests
	{
		[SetUp]
		public void Setup()
		{
			CommandRegistry.Reset();
			Format.Mode = Format.FormatMode.None;
		}

		#region Test Commands

		// Define command classes within each test to avoid registration conflicts

		public class StringParameterCommands
		{
			[Command("test", description: "String parameter command")]
			public void TestString(ICommandContext ctx, string value)
			{
				ctx.Reply($"String command executed with: {value}");
			}

			[Command("mixed")]
			public void MixedParams(ICommandContext ctx, string text, int number)
			{
				ctx.Reply($"Mixed command with string: {text}, int: {number}");
			}
		}

		public class IntParameterCommands
		{
			[Command("test", description: "Int parameter command")]
			public void TestInt(ICommandContext ctx, int value)
			{
				ctx.Reply($"Int command executed with: {value}");
			}

			[Command("selection")]
			public void Selection(ICommandContext ctx, int value)
			{
				ctx.Reply($"Int selection command with: {value}");
			}
		}

		public class FloatParameterCommands
		{
			[Command("test", description: "Float parameter command")]
			public void TestFloat(ICommandContext ctx, float value)
			{
				ctx.Reply($"Float command executed with: {value}");
			}

			[Command("selection")]
			public void Selection(ICommandContext ctx, float value)
			{
				ctx.Reply($"Float selection command with: {value}");
			}
		}

		#endregion

		#region Basic Overloading Tests

		[Test]
		public void OverloadedCommand_StringParameter_ExecutesCorrectCommand()
		{
			// Register just string command
			CommandRegistry.RegisterCommandType(typeof(StringParameterCommands));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".test hello");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("String command executed with: hello");
		}

		[Test]
		public void OverloadedCommand_IntParameter_ExecutesCorrectCommand()
		{
			// Register just int command
			CommandRegistry.RegisterCommandType(typeof(IntParameterCommands));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".test 123");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("Int command executed with: 123");
		}

		[Test]
		public void OverloadedCommand_FloatParameter_ExecutesCorrectCommand()
		{
			// Register just float command
			CommandRegistry.RegisterCommandType(typeof(FloatParameterCommands));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".test 123.45");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("Float command executed with: 123.45");
		}

		#endregion

		#region Multiple Valid Commands Tests

		[Test]
		public void MultipleValidCommands_ShowsSelectionOptions()
		{
			// Register both int and float commands
			CommandRegistry.RegisterCommandType(typeof(IntParameterCommands));
			CommandRegistry.RegisterCommandType(typeof(FloatParameterCommands));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".selection 42");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("Multiple commands match this input");
			ctx.AssertReplyContains("selection");
		}

		[Test]
		public void CommandSelection_SelectsCorrectCommand()
		{
			// Register both command types
			CommandRegistry.RegisterCommandType(typeof(IntParameterCommands));
			CommandRegistry.RegisterCommandType(typeof(FloatParameterCommands));

			var ctx = new AssertReplyContext();
			// First trigger selection
			CommandRegistry.Handle(ctx, ".selection 42");

			// Now select the first command
			var result = CommandRegistry.Handle(ctx, ".1");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("selection command with: 42");
		}

		[Test]
		public void CommandSelection_InvalidIndex_ReturnsError()
		{
			// Register both command types
			CommandRegistry.RegisterCommandType(typeof(IntParameterCommands));
			CommandRegistry.RegisterCommandType(typeof(FloatParameterCommands));

			var ctx = new AssertReplyContext();
			// First trigger selection
			CommandRegistry.Handle(ctx, ".selection 42");

			// Try an invalid selection
			var result = CommandRegistry.Handle(ctx, ".99");

			Assert.That(result, Is.EqualTo(CommandResult.UsageError));
			ctx.AssertReplyContains("Invalid selection");
		}

		#endregion

		#region Conversion Failure Tests

		// Custom commands for the conversion failure test
		public class FailingCommandClass
		{
			public class CustomType { }

			[Command("failing")]
			public void FailingCommand(ICommandContext ctx, CustomType value)
			{
				// This implementation will never be called
				ctx.Reply("This shouldn't execute");
			}
		}

		[Test]
		public void ConversionFailure_ShowsError()
		{
			CommandRegistry.RegisterCommandType(typeof(FailingCommandClass));

			var ctx = new AssertReplyContext();
			// Provide a command where no converter exists
			var result = CommandRegistry.Handle(ctx, ".failing 123");

			Assert.That(result, Is.EqualTo(CommandResult.Unmatched));
		}

		#endregion

		#region Complex Parameter Tests

		[Test]
		public void MixedParameterTypes_CorrectlyConverted()
		{
			CommandRegistry.RegisterCommandType(typeof(StringParameterCommands));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".mixed hello 42");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("Mixed command with string: hello, int: 42");
		}

		[Test]
		public void MixedParameterTypes_ConversionFailure()
		{
			CommandRegistry.RegisterCommandType(typeof(StringParameterCommands));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".mixed hello world");

			Assert.That(result, Is.EqualTo(CommandResult.UsageError));
			ctx.AssertReplyContains("[error]");
		}

		#endregion

		#region Command Overloading Edge Cases

		public class CommandsWithVaryingParameterCounts
		{
			[Command("params")]
			public void NoParams(ICommandContext ctx)
			{
				ctx.Reply("No parameters");
			}

			[Command("params")]
			public void OneParam(ICommandContext ctx, int value)
			{
				ctx.Reply($"One parameter: {value}");
			}

			[Command("params")]
			public void TwoParams(ICommandContext ctx, int a, int b)
			{
				ctx.Reply($"Two parameters: {a}, {b}");
			}
		}

		[Test]
		public void DifferentParameterCounts_SelectsCorrectOverload()
		{
			CommandRegistry.RegisterCommandType(typeof(CommandsWithVaryingParameterCounts));

			var ctx = new AssertReplyContext();

			// Test with no parameters
			var result1 = CommandRegistry.Handle(ctx, ".params");
			Assert.That(result1, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("No parameters");

			// Test with one parameter
			var ctx2 = new AssertReplyContext();
			var result2 = CommandRegistry.Handle(ctx2, ".params 42");
			Assert.That(result2, Is.EqualTo(CommandResult.Success));
			ctx2.AssertReplyContains("One parameter: 42");

			// Test with two parameters
			var ctx3 = new AssertReplyContext();
			var result3 = CommandRegistry.Handle(ctx3, ".params 10 20");
			Assert.That(result3, Is.EqualTo(CommandResult.Success));
			ctx3.AssertReplyContains("Two parameters: 10, 20");
		}

		[Test]
		public void TooManyParameters_ShowsError()
		{
			CommandRegistry.RegisterCommandType(typeof(CommandsWithVaryingParameterCounts));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".params 10 20 30");

			Assert.That(result, Is.EqualTo(CommandResult.UsageError));
		}

		#endregion

		#region Optional Parameter Tests

		public class CommandsWithOptionalParams
		{
			[Command("optional")]
			public void OptionalParam(ICommandContext ctx, string required, int optional = 42)
			{
				ctx.Reply($"Optional command: {required}, {optional}");
			}
		}

		[Test]
		public void OptionalParameters_CanBeOmitted()
		{
			CommandRegistry.RegisterCommandType(typeof(CommandsWithOptionalParams));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".optional hello");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("Optional command: hello, 42");
		}

		[Test]
		public void OptionalParameters_CanBeProvided()
		{
			CommandRegistry.RegisterCommandType(typeof(CommandsWithOptionalParams));

			var ctx = new AssertReplyContext();
			var result = CommandRegistry.Handle(ctx, ".optional hello 99");

			Assert.That(result, Is.EqualTo(CommandResult.Success));
			ctx.AssertReplyContains("Optional command: hello, 99");
		}

		#endregion
	}
}
