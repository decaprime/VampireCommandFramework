using ProjectM.Network;
using System;
using System.Collections.Generic;

namespace VampireCommandFramework.Framework;

// Serializes outgoing chat replies per user at most one per server frame.
//
// V Rising batches multiple chat messages created in the same server tick into
// one network snapshot, and the client has been observed to render that batch
// in a reshuffled order (e.g. '.2 - ModB' above '.1 - ModA' for a paginated
// disambiguation reply). Forcing at most one outbound message per user per
// server frame ensures each message lands in its own snapshot and therefore
// cannot be reordered against another message from the same conversation.
//
// Type erasure: the queue value carries the sender's User struct for the drain
// path, but this module deliberately stores it as `object` (boxed) rather than
// `ProjectM.Network.User`. Referencing the IL2CPP-interop User type in static
// field metadata would force the test host (where the Unity runtime is not
// initialized) to fail class loading with TypeLoadException on every
// ChatMessageQueueTests entry. The production SendSink in ChatDrainPatch.cs
// unboxes `object` back to `User` at send time; tests store `null` and their
// replacement SendSink ignores the user parameter entirely.
//
// Threading: this module is only ever touched from the server main thread
// (Harmony postfix on ServerBootstrapSystem.OnUpdate + synchronous Reply calls
// from command handlers). No locks.
internal static class ChatMessageQueue
{
	// Keyed on User.PlatformId. Value carries the boxed User alongside its
	// pending message queue so the drain path has the user in hand without a
	// side-map lookup. See the "Type erasure" note above for why User is
	// stored as object here.
	internal static readonly Dictionary<ulong, (object User, Queue<string> Queue)> _queues = new();
	internal static readonly HashSet<ulong> _sentThisTick = new();

	// Production wires this to a ServerChatUtils-based sink in
	// ChatDrainPatch.Install() that unboxes `object` to `User`. Tests overwrite
	// it with a collector lambda that ignores the user argument.
	internal static Action<object, string> SendSink = static (_, _) => { };

	// Production entry point, called from ChatCommandContext.Reply with the
	// real User struct. Boxes the User here so the call site just passes the
	// typed struct; the shared core stores it as `object` in the tuple.
	internal static void Send(User user, string message)
	{
		Send(user.PlatformId, user, message);
	}

	// Test-only convenience: tests never have a real User to pass. Stores null
	// in the User slot and relies on the test SendSink ignoring it.
	internal static void Send(ulong platformId, string message)
	{
		Send(platformId, null, message);
	}

	// Shared core. The `user` object is stored as-is in the queue tuple and
	// handed back to the SendSink at drain time. Production passes a boxed
	// User; tests pass null.
	private static void Send(ulong platformId, object user, string message)
	{
		var canFastPath =
			!_sentThisTick.Contains(platformId)
			&& (!_queues.TryGetValue(platformId, out var existing) || existing.Queue.Count == 0);

		if (canFastPath)
		{
			SendSink(user, message);
			_sentThisTick.Add(platformId);
			return;
		}

		if (!_queues.TryGetValue(platformId, out var entry))
		{
			entry = (user, new Queue<string>());
		}
		else
		{
			// Refresh the stored User — a reconnect may have handed us a
			// different component value under the same PlatformId.
			entry = (user, entry.Queue);
		}
		entry.Queue.Enqueue(message);
		_queues[platformId] = entry;
	}

	internal static void DrainOneTick()
	{
		_sentThisTick.Clear();

		if (_queues.Count == 0) return;

		// Snapshot keys because we may remove entries from the dictionary as
		// queues empty out.
		var platformIds = new List<ulong>(_queues.Keys);
		foreach (var platformId in platformIds)
		{
			if (!_queues.TryGetValue(platformId, out var entry) || entry.Queue.Count == 0)
			{
				_queues.Remove(platformId);
				continue;
			}

			var message = entry.Queue.Dequeue();
			SendSink(entry.User, message);
			_sentThisTick.Add(platformId);

			if (entry.Queue.Count == 0) _queues.Remove(platformId);
		}
	}

	internal static void Clear(ulong platformId)
	{
		_queues.Remove(platformId);
		_sentThisTick.Remove(platformId);
	}

	internal static void ResetForTests()
	{
		_queues.Clear();
		_sentThisTick.Clear();
		SendSink = static (_, _) => { };
	}
}
