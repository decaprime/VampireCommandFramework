using BepInEx.Logging;

namespace VampireCommandFramework;

// TODO: replace with 
public static class Log
{
	internal static ManualLogSource Instance { get; set; }

	public static void Warning(string s) => Instance?.LogWarning(s);
	public static void Error(string s) => Instance?.LogError(s);
	public static void Debug(string s) => Instance.LogDebug(s);
}
