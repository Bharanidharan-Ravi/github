using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class TicketStatusResult
    {
        // ── Computed values ───────────────────────────────────────────────────────
        public int ComputedStatusId { get; set; }
        public int? OldStatusId { get; set; }
        public string ComputedStatusName { get; set; } = string.Empty;
        public decimal OverallPct { get; set; }
        public bool TicketAutoCompleted { get; set; }
        public int? TotalSubtasks { get; set; }
        public int CompletedSubtasks { get; set; }
        public int ActiveSubtasks { get; set; }

        // ── Broadcast data — resolved inside service, consumed by Business Layer ──
        // Service reads RepoKey from DB so the repo doesn't need to do a second lookup
        public string RepoKey { get; set; } = string.Empty;

        // True when ticket was already Closed/Cancelled before this update
        // Business Layer uses this to skip the broadcast
        public bool IsTerminal { get; set; }

        // Pre-built payload so Business Layer just passes this to BroadcastAsync
        public object? BroadcastPayload { get; set; }
    }
}
