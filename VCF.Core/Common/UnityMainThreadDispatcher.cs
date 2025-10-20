using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM.Physics;
using System;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace VampireCommandFramework.Common;

/// <summary>
/// Unity-based main thread dispatcher using MonoBehaviour.
/// Allows safe execution of Unity/ECS operations from background threads.
/// </summary>
public static class UnityMainThreadDispatcher
{
    static readonly ConcurrentQueue<Action> _actionQueue = new();
	static MonoBehaviour monoBehaviour;

	/// <summary>
	/// Queue an action to be executed on the main thread during the next Update cycle
	/// </summary>
	public static void Enqueue(Action action)
    {
        if (action == null) return;

		if (monoBehaviour == null)
		{
			var go = new GameObject("VampireCommandFramework");
			monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
			UnityEngine.Object.DontDestroyOnLoad(go);
			monoBehaviour.StartCoroutine(RunOnMainThread().WrapToIl2Cpp());
		}

		_actionQueue.Enqueue(action);
    }

    static IEnumerator RunOnMainThread()
    {
		while (true)
		{
			yield return null;
			while (_actionQueue.TryDequeue(out var action))
			{
				try
				{
					action.Invoke();
				}
				catch (Exception ex)
				{
					Log.Error($"Error executing main thread action: {ex.Message}");
				}
			}
		}
    }
}
