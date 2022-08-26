using System.Reflection;
using VampireCommandFramework;

namespace VCF.Core.Basics;

public class BasicAdminCheck : CommandMiddleware
{
    public override bool CanExecute(ICommandContext ctx, ChatCommandAttribute cmd, MethodInfo m) => !cmd.AdminOnly || ctx.IsAdmin;
}
