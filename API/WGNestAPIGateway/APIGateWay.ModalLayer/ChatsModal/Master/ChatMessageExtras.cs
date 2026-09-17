using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>One user's emoji on a message. Unique on (MessageId, UserId, Emoji).</summary>
    [Table("ChatMessageReactions")]
    public class ChatMessageReaction
    {
        [Key]
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public string Emoji { get; set; } = string.Empty;

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Unencrypted reference from a message to a WGNest entity (@user, #ticket, ...).
    /// The token itself stays inside the encrypted payload; this row only powers
    /// notifications and "mentioned in" lookups.
    /// </summary>
    [Table("ChatMessageTags")]
    public class ChatMessageTag
    {
        [Key]
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }

        /// <summary>User | Ticket | Meeting | Project | Repo</summary>
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
    }

    /// <summary>
    /// An encrypted file or voice note stored on local server disk. The file key is
    /// encrypted with the message content key, so only conversation members can read it.
    /// </summary>
    [Table("ChatMediaAttachments")]
    public class ChatMediaAttachment
    {
        [Key]
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public byte[] EncryptedFileKey { get; set; } = Array.Empty<byte>();

        /// <summary>12-byte AES-GCM nonce used for the file bytes.</summary>
        public byte[] Iv { get; set; } = Array.Empty<byte>();

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }
    }

    public static class ChatTagEntityType
    {
        public const string User = "User";
        public const string Ticket = "Ticket";
        public const string Meeting = "Meeting";
        public const string Project = "Project";
        public const string Repo = "Repo";

        public static readonly string[] All = { User, Ticket, Meeting, Project, Repo };
    }
}
