using NUnit.Framework;
using VampireCommandFramework.Framework;

namespace VCF.Tests;

// Covers the mechanics of ChatMessageQueue — the fast-path / enqueue decision,
// the one-per-user-per-tick drain, and Clear. Tests use the Send(ulong, string)
// overload so they never have to fabricate a ProjectM.Network.User (which would
// need an initialized IL2CPP runtime). The test SendSink ignores the User it
// receives and records only the message text.
public class ChatMessageQueueTests
{
	private List<string> _sent = null!;

	[SetUp]
	public void SetUp()
	{
		ChatMessageQueue.ResetForTests();
		_sent = new List<string>();
		ChatMessageQueue.SendSink = (_, message) => _sent.Add(message);
	}

	[TearDown]
	public void TearDown()
	{
		ChatMessageQueue.ResetForTests();
	}

	[Test]
	public void Send_FirstMessage_SendsImmediately()
	{
		ChatMessageQueue.Send(1, "a");

		Assert.That(_sent, Is.EqualTo(new[] { "a" }));
		Assert.That(ChatMessageQueue._queues.ContainsKey(1), Is.False);
		Assert.That(ChatMessageQueue._sentThisTick, Contains.Item(1UL));
	}

	[Test]
	public void Send_SecondMessageSameTick_Queues()
	{
		ChatMessageQueue.Send(1, "a");
		ChatMessageQueue.Send(1, "b");

		Assert.That(_sent, Is.EqualTo(new[] { "a" }));
		Assert.That(ChatMessageQueue._queues.ContainsKey(1), Is.True);
		Assert.That(ChatMessageQueue._queues[1].Queue, Is.EqualTo(new[] { "b" }));
	}

	[Test]
	public void Send_SameTickDifferentUsers_BothSendImmediately()
	{
		ChatMessageQueue.Send(1, "a");
		ChatMessageQueue.Send(2, "x");

		Assert.That(_sent, Is.EqualTo(new[] { "a", "x" }));
		Assert.That(ChatMessageQueue._queues, Is.Empty);
		Assert.That(ChatMessageQueue._sentThisTick, Is.EquivalentTo(new[] { 1UL, 2UL }));
	}

	[Test]
	public void DrainOneTick_ClearsFlagsAndSendsOnePerUser()
	{
		// Each user: first Send fast-paths, three more queue up. So each
		// queue should end up with 3 pending messages.
		ChatMessageQueue.Send(1, "a1");
		ChatMessageQueue.Send(1, "a2");
		ChatMessageQueue.Send(1, "a3");
		ChatMessageQueue.Send(1, "a4");
		ChatMessageQueue.Send(2, "b1");
		ChatMessageQueue.Send(2, "b2");
		ChatMessageQueue.Send(2, "b3");
		ChatMessageQueue.Send(2, "b4");

		Assert.That(_sent, Is.EqualTo(new[] { "a1", "b1" }));
		Assert.That(ChatMessageQueue._queues[1].Queue.Count, Is.EqualTo(3));
		Assert.That(ChatMessageQueue._queues[2].Queue.Count, Is.EqualTo(3));

		_sent.Clear();
		ChatMessageQueue.DrainOneTick();

		// One additional send per user, FIFO: a2 and b2.
		Assert.That(_sent, Is.EquivalentTo(new[] { "a2", "b2" }));
		Assert.That(ChatMessageQueue._queues[1].Queue.Count, Is.EqualTo(2));
		Assert.That(ChatMessageQueue._queues[2].Queue.Count, Is.EqualTo(2));
		Assert.That(ChatMessageQueue._sentThisTick, Is.EquivalentTo(new[] { 1UL, 2UL }));
	}

	[Test]
	public void DrainOneTick_FollowedBySendSameTick_Queues()
	{
		// Simulates the real server frame sequence:
		//   (a) DrainOneTick runs early in the frame.
		//   (b) Later in the same frame, a command handler calls ctx.Reply
		//       → ChatMessageQueue.Send. It must see that this user already
		//       used their send this frame and enqueue instead.
		ChatMessageQueue.Send(1, "pre");  // queue nothing; just mark user 1
		Assert.That(ChatMessageQueue._sentThisTick, Contains.Item(1UL));

		// Simulate the start of the next server frame: DrainOneTick clears
		// _sentThisTick at the top. Queue is empty, so no send happens.
		_sent.Clear();
		ChatMessageQueue.DrainOneTick();
		Assert.That(_sent, Is.Empty);
		Assert.That(ChatMessageQueue._sentThisTick, Is.Empty);

		// First Reply of the new frame: fast-path send.
		ChatMessageQueue.Send(1, "frame2-first");
		Assert.That(_sent, Is.EqualTo(new[] { "frame2-first" }));

		// Second Reply same frame: queue because flag is now set.
		ChatMessageQueue.Send(1, "frame2-second");
		Assert.That(_sent, Is.EqualTo(new[] { "frame2-first" }));
		Assert.That(ChatMessageQueue._queues[1].Queue, Is.EqualTo(new[] { "frame2-second" }));
	}

	[Test]
	public void DrainOneTick_Empties_RemovesQueueEntry()
	{
		ChatMessageQueue.Send(1, "a");
		ChatMessageQueue.Send(1, "b");
		Assert.That(ChatMessageQueue._queues[1].Queue.Count, Is.EqualTo(1));

		// Next frame drains 'b', queue becomes empty → entry removed.
		ChatMessageQueue.DrainOneTick();

		Assert.That(_sent, Is.EqualTo(new[] { "a", "b" }));
		Assert.That(ChatMessageQueue._queues.ContainsKey(1), Is.False);
	}

	[Test]
	public void Clear_DropsQueueAndFlag()
	{
		ChatMessageQueue.Send(1, "a");
		ChatMessageQueue.Send(1, "b");
		ChatMessageQueue.Send(1, "c");

		ChatMessageQueue.Clear(1);

		Assert.That(ChatMessageQueue._queues.ContainsKey(1), Is.False);
		Assert.That(ChatMessageQueue._sentThisTick, Does.Not.Contain(1UL));
	}

	[Test]
	public void Send_AfterClear_IsImmediate()
	{
		ChatMessageQueue.Send(1, "a");
		ChatMessageQueue.Send(1, "b");
		ChatMessageQueue.Clear(1);

		_sent.Clear();
		ChatMessageQueue.Send(1, "fresh");

		Assert.That(_sent, Is.EqualTo(new[] { "fresh" }));
		Assert.That(ChatMessageQueue._queues.ContainsKey(1), Is.False);
		Assert.That(ChatMessageQueue._sentThisTick, Contains.Item(1UL));
	}
}
