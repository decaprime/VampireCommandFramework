namespace VampireCommandFramework
{
	public abstract class ChatCommandArgumentConverter<T>
	{
		public abstract bool TryParse(CommandContext ctx, string input, out T result);
	}
}
