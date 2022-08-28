using System.Reflection;
using VampireCommandFramework;

namespace VCF.Core.Basics;

public class BasicAdminCheck : CommandMiddleware
{
	public override bool CanExecute(ICommandContext ctx, ChatCommandAttribute cmd, MethodInfo m)
	{
		Log.Debug($"Running BasicAdmin Check adminOnly: {cmd.AdminOnly} IsAdmin: {ctx.IsAdmin}");
		return !cmd.AdminOnly || ctx.IsAdmin;
	}
}
