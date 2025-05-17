using VampireCommandFramework;

namespace VampireCommandFramework.Basics
{
	public static class RepeatCommands
	{
		[Command("!", description: "Repeats the most recently executed command")]
		public static void RepeatLastCommand(ICommandContext ctx)
		{
			// This is just a placeholder for the help system
			// The actual implementation is in CommandRegistry.HandleCommandHistory
			ctx.Error("This command is only a placeholder for the help system.");
		}

		[Command("! list", shortHand: "! l", description: "Lists up to the last 10 commands you used.")]
		public static void ListCommandHistory(ICommandContext ctx)
		{
			// This is just a placeholder for the help system
			// The actual implementation is in CommandRegistry.HandleCommandHistory
			ctx.Error("This command is only a placeholder for the help system.");
		}

		[Command("!", description: "Executes a specific command from your history by its number")]
		public static void ExecuteHistoryCommand(ICommandContext ctx, int previousXCommand)
		{
			ctx.Error("This command is only a placeholder for the help system.");
		}
	}
}
