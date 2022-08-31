using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using UnityEngine;

namespace VampireCommandFramework.Breadstone;

/// <summary>
/// Various utilities for interacting with the Unity ECS world.
/// </summary>
internal static class VWorld
{
	public static void SendSystemMessage(this User user, string message)
	{
		ServerChatUtils.SendSystemMessageToClient(Server.EntityManager, user, message);
	}

	private static World _serverWorld;

	/// <summary>
	/// Return the Unity ECS World instance used on the server build of VRising.
	/// </summary>
	public static World Server
	{
		get
		{
			if (_serverWorld != null) return _serverWorld;

			_serverWorld = GetWorld("Server")
				?? throw new System.Exception("There is no Server world (yet). Did you install a server mod on the client?");
			return _serverWorld;
		}
	}

	/// <summary>
	/// Return whether we're currently running on the server build of VRising.
	/// </summary>
	public static bool IsServer => Application.productName == "VRisingServer";

	private static World GetWorld(string name)
	{
		foreach (var world in World.s_AllWorlds)
		{
			if (world.Name == name)
			{
				_serverWorld = world;
				return world;
			}
		}

		return null;
	}
}