using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.ChatsModal.Master
{
    /// <summary>
    /// Join table linking an application user to a <see cref="ChatRoom"/>.
    /// 
    /// NOTE: <c>UserId</c> is typed as <c>string</c> to match ASP.NET Core Identity's
    /// default ApplicationUser primary key.  If your project uses <c>int</c> or <c>Guid</c>
    /// user keys, update this property and the EF configuration accordingly.
    /// </summary>
    public class ChatParticipant
    {
        public Guid Id { get; set; }
        public Guid ChatRoomId { get; set; }

        /// <summary>
        /// Foreign key referencing your existing User / ApplicationUser table.
        /// Indexed alongside ChatRoomId for fast membership look-ups.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>UTC timestamp of when the user was added to this room.</summary>
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation ────────────────────────────────────────────────────────────
        public ChatRoom ChatRoom { get; set; } = null!;
    }
}