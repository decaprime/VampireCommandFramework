namespace VampireCommandFramework;

public abstract class CommandArgumentConverter<T> : CommandArgumentConverter<T, ICommandContext>
{
}

public abstract class CommandArgumentConverter<T, C> where C : ICommandContext
{
	public abstract T Parse(C ctx, string input);
}
