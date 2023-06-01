using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework.Common;

namespace VampireCommandFramework.Breadstone;

/// <summary>
/// Hook responsible for handling calls to IRunOnInitialized.
/// </summary>
internal static class OnInitialize
{
	internal static bool HasInitialized { get; private set; } = false;

	internal static void InvokePlugins()
	{
		Log.Info("Game has bootstrapped. Worlds and systems now exist.");

		if (HasInitialized) return;
		HasInitialized = true;

		foreach (var (name, info) in IL2CPPChainloader.Instance.Plugins)
		{
			if (info.Instance is IRunOnInitialized runOnInitialized)
			{
				runOnInitialized.OnGameInitialized();
			}
		}

		foreach (var plugin in Reload._loadedPlugins)
		{
			if (plugin is IRunOnInitialized runOnInitialized)
			{
				runOnInitialized.OnGameInitialized();
			}
		}
	}
}

[HarmonyPatch("ProjectM.GameBootstrap", "Start")]
public static class SeverDetours
{
	// use of string here is intentional, avoids issues if the class somehow does not exist
	[HarmonyPostfix]
	public static void Postfix()
	{
		OnInitialize.InvokePlugins();
	}
}