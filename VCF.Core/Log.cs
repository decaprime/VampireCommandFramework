using BepInEx.Logging;
using System;

namespace VampireCommandFramework;

// TODO: replace with 
public static class Log
{
	internal static ManualLogSource Instance { get; set; }

	public static void Warning(string s) => LogOrConsole(s, s => Instance.LogWarning(s));
	public static void Error(string s) => LogOrConsole(s, s => Instance.LogError(s));
	public static void Debug(string s) => LogOrConsole(s, s => Instance.LogDebug(s));


	private static void LogOrConsole(string message, Action<string> instanceLog)
	{
		if (Instance == null)
		{
			Console.WriteLine(message);
		}
		else
		{
			instanceLog(message);
		}
	}
}
