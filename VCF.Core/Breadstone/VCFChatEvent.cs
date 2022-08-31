using ProjectM.Network;
using Unity.Entities;

namespace VampireCommandFramework.Breadstone;

/// <summary>
/// Represents a chat message sent by a user.
/// </summary>
public class VChatEvent
{
	/// <summary>
	/// The user entity of the user that sent the message. This contains the `User` component.
	/// </summary>
	public Entity SenderUserEntity { get; }
	/// <summary>
	/// The character entity of the user that sent the message. This contains the character
	/// instances, such as its position, health, etc.
	/// </summary>
	public Entity SenderCharacterEntity { get; }
	/// <summary>
	/// The message that was sent.
	/// </summary>
	public string Message { get; }
	/// <summary>
	/// The type of message that was sent.
	/// </summary>
	public ChatMessageType Type { get; }

	/// <summary>
	/// Whether this message was cancelled. Cancelled messages will not be
	/// forwarded to the normal VRising chat system and will not be sent to
	/// any other clients. Use the Cancel() function to set this flag. Note
	/// that cancelled events will still be forwarded to other plugins that
	/// have subscribed to this event.
	/// </summary>
	public bool Cancelled { get; private set; } = false;

	/// <summary>
	/// The user component instance of the user that sent the message.
	/// </summary>
	public User User { get; }

	internal VChatEvent(Entity userEntity, Entity characterEntity, string message, ChatMessageType type, User user)
	{
		SenderUserEntity = userEntity;
		SenderCharacterEntity = characterEntity;
		Message = message;
		Type = type;
		User = user;
	}

	/// <summary>
	/// Cancel this message. Cancelled messages will not be forwarded to the
	/// normal VRising chat system and will not be sent to any other clients.
	/// Note that cancelled events will still be forwarded to other plugins 
	/// that have subscribed to this event.
	/// </summary>
	public void Cancel()
	{
		Cancelled = true;
	}
}
