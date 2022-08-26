using System.Reflection;

namespace VampireCommandFramework;

public abstract class CommandMiddleware
{
	public virtual bool CanExecute(ICommandContext ctx, ChatCommandAttribute attribute, MethodInfo method) { return true; }
	// the difference should be between can I run this command and I"m running this command that I'm allowed to now, are you going to stop me?
	public virtual void BeforeExecute(ICommandContext ctx, ChatCommandAttribute attribute, MethodInfo method) { }
	public virtual void AfterExecute(ICommandContext ctx, ChatCommandAttribute attribute, MethodInfo method) { }
}
