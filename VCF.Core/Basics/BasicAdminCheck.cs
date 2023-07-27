using System.Reflection;
using VampireCommandFramework;
using VampireCommandFramework.Common;

namespace VCF.Core.Basics;

public class BasicAdminCheck : CommandMiddleware
{
	public override bool CanExecute(ICommandContext ctx, CommandAttribute cmd, MethodInfo m)
	{
		// Log.Debug($"Running BasicAdmin Check adminOnly: {cmd.AdminOnly} IsAdmin: {ctx.IsAdmin}");
		return !cmd.AdminOnly || ctx.IsAdmin;
	}
}
