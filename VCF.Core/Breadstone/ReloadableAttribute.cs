namespace VampireCommandFramework.Breadstone;

/// <summary>
/// Plugins that implement this interface will be reloaded when the
/// .reload VCF is executed. This is intended for development only
/// </summary>
/// <remarks>
/// This is intended for mods that don't use Wetstone. This was included
/// during Gloomrot compatibility work to speed up development.
/// 
/// Expect this might change without a major version bump as it's experimental
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Class)]
public class ReloadableAttribute : System.Attribute
{

}
