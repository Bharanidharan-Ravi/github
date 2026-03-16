using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class TicketStatusResult
    {
        // ── Computed status ───────────────────────────────────────────────────
        // The Status_Master.Id to set on the ticket
        public int ComputedStatusId { get; set; }

        // Human-readable label from Status_Master.Status_Name
        public string ComputedStatusName { get; set; } = string.Empty;

        // ── Computed percentage ───────────────────────────────────────────────
        // Simple average of all non-inactive subtask CompletionPct values
        // e.g. subtasks at 100, 80, 50, 20, 0 → OverallPct = 50.00
        public decimal OverallPct { get; set; }

        // ── Whether ticket was auto-completed ─────────────────────────────────
        public bool TicketAutoCompleted { get; set; }

        // ── Breakdown — for debugging / frontend display ──────────────────────
        public int? TotalSubtasks { get; set; }  // non-inactive count
        public int CompletedSubtasks { get; set; }  // in CompletedStatuses
        public int ActiveSubtasks { get; set; }  // still in progress
    }
}
