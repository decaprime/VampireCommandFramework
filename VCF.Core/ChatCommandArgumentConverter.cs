namespace VampireCommandFramework
{
	public abstract class ChatCommandArgumentConverter<T>
	{
		public abstract T Parse(CommandContext ctx, string input);
	}
}
