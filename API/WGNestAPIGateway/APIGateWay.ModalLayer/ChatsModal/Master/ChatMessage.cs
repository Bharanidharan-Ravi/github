using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// A single message within a <see cref="ChatRoom"/>.
    /// 
    /// Design decisions:
    /// • <c>SenderName</c> is intentionally denormalized to avoid expensive joins when
    ///   rendering message history. If the user's display name changes, historical
    ///   messages retain the name they were sent under — which is usually the desired UX.
    /// • <c>IsDeleted</c> is a soft-delete flag; the row is kept so thread continuity
    ///   is preserved and clients can render "[Message deleted]" appropriately.
    /// </summary>
    public class ChatMessage
    {
        public Guid Id { get; set; }
        public Guid ChatRoomId { get; set; }

        /// <summary>UserId of the message author.</summary>
        public string SenderId { get; set; } = string.Empty;

        /// <summary>
        /// Denormalized display name stored at write time.
        /// Avoids joins on every message fetch and preserves the historical name.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>Raw message content. Max 4 000 characters, enforced at the hub layer.</summary>
        public string MessageText { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When true, the message body is hidden from clients (soft-delete).
        /// The row is retained to avoid gaps in pagination offsets.
        /// </summary>
        public bool IsDeleted { get; set; }

        // ── Navigation ────────────────────────────────────────────────────────────
        public ChatRoom ChatRoom { get; set; } = null!;
    }

}