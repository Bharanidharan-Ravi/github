using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.Hub
{
    public class RealtimeMessage
    {
        /// <summary>Entity type identifier.  Must match a key in the frontend dispatcher's registry.</summary>
        public string Entity { get; set; } = string.Empty;

        /// <summary>"Create" | "Update" | "Delete"</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>The serialized entity object (rich DTO).</summary>
        public object Payload { get; set; } = new();

        /// <summary>The property name on Payload that uniquely identifies the record (e.g. "Issue_Id").</summary>
        public string KeyField { get; set; } = string.Empty;

        /// <summary>Repo identifier – used to route to "repo-{RepoKey}" SignalR group.</summary>
        public string? RepoKey { get; set; }

        /// <summary>
        /// Issue / Ticket GUID – required for ThreadsList events so the frontend
        /// knows which per-ticket thread cache to update.
        /// </summary>
        public Guid? IssueId { get; set; }

        /// <summary>
        /// Optional: target a specific user's connection directly.
        /// Useful for personal notifications (assignment, mention, etc.).
        /// </summary>
        public Guid? TargetUserId { get; set; }

        /// <summary>UTC timestamp – used for client-side message deduplication.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
