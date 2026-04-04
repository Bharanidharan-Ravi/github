using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    // =========================================================================
    // HistoryEventType — string constants for EventType column
    // Use these everywhere — never hardcode strings
    // =========================================================================
    public static class HistoryEventType
    {
        // ── Ticket lifecycle ──────────────────────────────────────────────────
        public const string TicketCreated = "TICKET_CREATED";
        public const string TicketUpdated = "TICKET_UPDATED";
        public const string StatusChanged = "STATUS_CHANGED";
        public const string TicketClosed = "TICKET_CLOSED";
        public const string TicketCancelled = "TICKET_CANCELLED";

        // ── People ────────────────────────────────────────────────────────────
        public const string AssigneeAdded = "ASSIGNEE_ADDED";
        public const string AssigneeRemoved = "ASSIGNEE_REMOVED";

        // ── Labels ────────────────────────────────────────────────────────────
        public const string LabelAdded = "LABEL_ADDED";
        public const string LabelRemoved = "LABEL_REMOVED";

        // ── WorkStream (Subtask) ──────────────────────────────────────────────
        public const string WorkStreamCreated = "WORKSTREAM_CREATED";
        public const string WorkStreamUpdated = "WORKSTREAM_UPDATED";
        public const string WorkStreamCompleted = "WORKSTREAM_COMPLETED";
        public const string WorkStreamInactive = "WORKSTREAM_INACTIVE";
        public const string OverallPctChanged = "OVERALL_PCT_CHANGED";

        // ── Test failure ──────────────────────────────────────────────────────
        public const string TestFailureReported = "TEST_FAILURE_REPORTED";
        public const string TestFailureCleared = "TEST_FAILURE_CLEARED";

        // ── Thread / attachment ───────────────────────────────────────────────
        public const string ThreadPosted = "THREAD_POSTED";
        public const string AttachmentAdded = "ATTACHMENT_ADDED";

        // ── Daily plan ────────────────────────────────────────────────────────
        public const string DailyPlanAdded = "DAILY_PLAN_ADDED";
        public const string DailyPlanCompleted = "DAILY_PLAN_COMPLETED";
        public const string DailyPlanUnchecked = "DAILY_PLAN_UNCHECKED";
    }
}
