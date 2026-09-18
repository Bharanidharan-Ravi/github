using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    [Table("ChatConversations")]
    public class ChatConversation
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>1 = Direct, 2 = Group</summary>
        public ChatConversationType Type { get; set; } = ChatConversationType.Direct;

        /// <summary>Group name; null for Direct conversations.</summary>
        public string? Title { get; set; }

        /// <summary>Group photo URL; null for Direct conversations and groups without one set.</summary>
        public string? GroupIconUrl { get; set; }

        /// <summary>"minUserId|maxUserId" for Direct conversations (unique); null for Group.</summary>
        public string? DirectKey { get; set; }

        public Guid CreatedByUserId { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Composite key (ConversationId, UserId) — configured in APIGatewayDBContext.</summary>
    [Table("ChatConversationMembers")]
    public class ChatConversationMember
    {
        public Guid ConversationId { get; set; }
        public Guid UserId { get; set; }

        /// <summary>Admin | Member</summary>
        public string Role { get; set; } = ChatMemberRole.Member;

        [Column(TypeName = "datetime2")]
        public DateTime JoinedAt { get; set; }

        /// <summary>Messages created after this are unread for this member; null = never opened.</summary>
        [Column(TypeName = "datetime2")]
        public DateTime? LastReadAt { get; set; }
    }

    public enum ChatConversationType : byte
    {
        Direct = 1,
        Group = 2,
    }

    public static class ChatMemberRole
    {
        public const string Admin = "Admin";
        public const string Member = "Member";
    }
}
