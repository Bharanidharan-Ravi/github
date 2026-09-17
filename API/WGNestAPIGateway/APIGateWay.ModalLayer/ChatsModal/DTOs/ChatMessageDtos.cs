using System;
using System.Collections.Generic;

namespace APIGateWay.ModalLayer.ChatsModal.DTOs
{
    public class OpenDirectConversationDto
    {
        public Guid UserId { get; set; }
    }

    public class ConversationDto
    {
        public Guid ConversationId { get; set; }
        public string Type { get; set; } = string.Empty;
        public List<Guid> MemberUserIds { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
    }

    public class MessageKeyDto
    {
        public Guid RecipientDeviceId { get; set; }
        public int PreKeyId { get; set; }
        public string WrappedKey { get; set; } = string.Empty;
    }

    public class SendMessageDto
    {
        public Guid ClientMessageId { get; set; }
        public Guid SenderDeviceId { get; set; }
        public string Ciphertext { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
        public string EphemeralPublicKey { get; set; } = string.Empty;

        /// <summary>The content key wrapped once per recipient device (including the sender's own devices).</summary>
        public List<MessageKeyDto> Keys { get; set; } = new();
    }

    public class ChatMessageDto
    {
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderUserId { get; set; }
        public Guid SenderDeviceId { get; set; }
        public Guid ClientMessageId { get; set; }
        public string Ciphertext { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
        public string EphemeralPublicKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>Only the wrapped keys for the caller's own device(s) — empty means this device can't read it.</summary>
        public List<MessageKeyDto> Keys { get; set; } = new();
    }
}
