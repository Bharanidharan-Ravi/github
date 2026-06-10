using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// Represents a conversation thread — either a 1-on-1 DirectMessage or a named Group chat.
    /// A ChatRoom is the root aggregate that owns both Participants and Messages.
    /// </summary>
    public class ChatRoom
    {
        public Guid Id { get; set; }

        /// <summary>
        /// For DirectMessage rooms, this is an auto-generated internal key.
        /// For Group rooms, this is the user-provided display name (max 120 chars).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public bool IsGroup { get; set; }
        public ChatRoomType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation Properties ─────────────────────────────────────────────────
        public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    /// <summary>Discriminates between a 1-on-1 DM and a named group conversation.</summary>
    public enum ChatRoomType
    {
        DirectMessage = 0,
        Group = 1,
    }
}