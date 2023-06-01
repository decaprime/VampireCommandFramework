using BepInEx.Logging;
using System;

namespace VampireCommandFramework.Common;

public static class Log
{
	public static ManualLogSource Instance { get; set; }

	public static void Warning(string s) => LogOrConsole(s, s => Instance.LogWarning(s));
	public static void Error(string s) => LogOrConsole(s, s => Instance.LogError(s));
	public static void Debug(string s) => LogOrConsole(s, s => Instance.LogDebug(s));
	public static void Info(string s) => LogOrConsole(s, s => Instance.LogInfo(s));



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
