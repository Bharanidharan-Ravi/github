using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// An end-to-end encrypted message. The server stores only ciphertext; the content
    /// key needed to decrypt it is wrapped per recipient device in <see cref="ChatMessageKey"/>.
    /// </summary>
    [Table("ChatEncryptedMessages")]
    public class ChatEncryptedMessage
    {
        [Key]
        public Guid MessageId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderUserId { get; set; }
        public Guid SenderDeviceId { get; set; }
        public Guid ClientMessageId { get; set; }
        public string Ciphertext { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
        public string EphemeralPublicKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    [Table("ChatMessageKeys")]
    public class ChatMessageKey
    {
        [Key]
        public Guid MessageKeyId { get; set; }
        public Guid MessageId { get; set; }
        public Guid RecipientUserId { get; set; }
        public Guid RecipientDeviceId { get; set; }
        public int PreKeyId { get; set; }
        public string WrappedKey { get; set; } = string.Empty;
    }
}
