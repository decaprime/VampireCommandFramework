namespace VampireCommandFramework;

public abstract class CommandArgumentConverter<T>
{
	public abstract T Parse(ICommandContext ctx, string input);
}
