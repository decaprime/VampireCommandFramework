using BepInEx.Unity.IL2CPP;
using ProjectM;
using ProjectM.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework.Breadstone;

namespace VampireCommandFramework.Common;


internal static class VersionChecker
{
	public static void ListAllPluginVersions(Entity userEntity = default)
	{
		try
		{
			// Get all loaded plugins
			var installedPlugins = GetInstalledPlugins();

			if (installedPlugins.Count == 0)
			{
				LogInfoAndSendMessageToClient(userEntity, "No plugins found.");
				return;
			}

			LogInfoAndSendMessageToClient(userEntity, $"Installed Plugins ({installedPlugins.Count}):");

			// Sort plugins by name for easier reading
			foreach (var plugin in installedPlugins.OrderBy(p => p.Name))
			{
				var pluginMessage = $"{plugin.Name.Color(Color.Command)}: {plugin.Version.Color(Color.Green)}";
				var formattedMessage = $"[vcf] ".Color(Color.Primary) + pluginMessage;
				SendMessageToClient(userEntity, formattedMessage);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"Error listing plugin versions: {ex.Message}");
		}
	}

	static void SendMessageToClient(Entity userEntity, string message)
	{
		if (userEntity == default) return;

		// Queue ECS operations for main thread execution to avoid IL2CPP threading issues
		UnityMainThreadDispatcher.Enqueue(() =>
		{
			try
			{
				// Now we're on main thread - safe to access ECS components
				if (VWorld.Server?.EntityManager == null) return;
				if (!VWorld.Server.EntityManager.Exists(userEntity)) return;
				if (!VWorld.Server.EntityManager.HasComponent<User>(userEntity)) return;

				var user = VWorld.Server.EntityManager.GetComponentData<User>(userEntity);
				if (!user.IsConnected) return;

				var msg = new FixedString512Bytes(message);
				ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, user, ref msg);
			}
			catch (Exception ex)
			{
				Log.Debug($"Could not send message to client (user may have disconnected): {ex.Message}");
			}
		});
	}

	static void LogInfoAndSendMessageToClient(Entity userEntity, string message)
	{
		Log.Info(message);
		SendMessageToClient(userEntity, message);
	}

	/// Gets information about all installed BepInEx plugins
	private static List<InstalledPluginInfo> GetInstalledPlugins()
	{
		var plugins = new List<InstalledPluginInfo>();

		foreach (var pluginKvp in IL2CPPChainloader.Instance.Plugins)
		{
			var pluginInfo = pluginKvp.Value;
			if (pluginInfo?.Metadata != null)
			{
				plugins.Add(new InstalledPluginInfo
				{
					GUID = pluginInfo.Metadata.GUID,
					Name = pluginInfo.Metadata.Name,
					Version = pluginInfo.Metadata.Version.ToString()
				});
			}
		}

		return plugins;
	}


	/// Information about an installed plugin
	private class InstalledPluginInfo
	{
		public string GUID { get; set; }
		public string Name { get; set; }
		public string Version { get; set; }
	}


}
