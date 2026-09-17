using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    [Table("ChatConversations")]
    public class ChatConversation
    {
        [Key]
        public Guid ConversationId { get; set; }

        /// <summary>Direct (Group comes later)</summary>
        public string Type { get; set; } = ChatConversationType.Direct;

        /// <summary>"smallerUserId|largerUserId" for Direct conversations; unique.</summary>
        public string? DirectKey { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
    }

    [Table("ChatConversationMembers")]
    public class ChatConversationMember
    {
        [Key]
        public Guid MemberId { get; set; }
        public Guid ConversationId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public static class ChatConversationType
    {
        public const string Direct = "Direct";
    }
}
