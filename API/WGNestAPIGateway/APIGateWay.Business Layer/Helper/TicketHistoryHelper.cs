using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper
{
    public class HistoryLabelDto
    {
        public long id { get; set; }
        public string name { get; set; }
    }
    public static class TicketHistoryHelper
    {
        // ─────────────────────────────────────────────────────────────────────
        // TICKET LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry TicketCreated(
            Guid? issueId,
            string issueCode,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId ?? Guid.Empty,
                EventType = HistoryEventType.TicketCreated,
                Summary = $"Ticket {issueCode} created",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry TicketUpdated(
            Guid issueId,
            string? fieldName,
            string? oldValue,
            string? newValue,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.TicketUpdated,
                Summary = $"{fieldName} changed from '{oldValue}' to '{newValue}'",
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry StatusChanged(
        Guid issueId,
        int? oldStatusId,           // 🔥 Reordered to keep ID and Name together
        string oldStatusName,
        int? newStatusId,           // 🔥 Added newStatusId
        string newStatusName,
        Guid actorId,
        string actorName,
        bool isAutoComputed = false)
        {
            var summary = isAutoComputed
                ? $"Status auto-updated from '{oldStatusName}' to '{newStatusName}'"
                : $"Status changed from '{oldStatusName}' to '{newStatusName}'";

            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.StatusChanged,
                Summary = summary,
                FieldName = "Status",
                OldValue = oldStatusName,
                NewValue = newStatusName,
                ActorId = actorId,
                ActorName = actorName,
                // 🔥 NEW: Rich JSON payload for the UI to render badges perfectly
                Meta = new
                {
                    previousState = new { id = oldStatusId, name = oldStatusName },
                    newState = new { id = newStatusId, name = newStatusName },
                    isAutoComputed = isAutoComputed
                }
            };
        }
        // Add inside TicketHistoryHelper.cs

        public static TicketHistoryEntry TicketClosedWithContext(
            Guid issueId,
            int? oldStatusId,
            string oldStatusName,
            int? newStatusId,
            string newStatusName,
            long? threadId, // Will be null if closed from Edit screen
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.TicketClosed,
                Summary = threadId.HasValue ? "Ticket closed with comment" : "Ticket closed directly",
                FieldName = "Status",
                OldValue = oldStatusName,
                NewValue = "Closed",
                ThreadId = threadId,
                ActorId = actorId,
                ActorName = actorName,
                // 🔥 Rich JSON for Timesheet
                Meta = new
                {
                    action = "closed",
                    closedAt = DateTime.UtcNow,
                    hasThread = threadId.HasValue,
                    threadId = threadId,
                    previousState = new { id = oldStatusId, name = oldStatusName },
                    newState = new { id = newStatusId, name = newStatusName }
                }
            };
        }

        public static TicketHistoryEntry TicketReopened(
            Guid issueId,
            int? newStatusId,
            string newStatusName,
            long? threadId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                // If you don't have HistoryEventType.TicketReopened, use HistoryEventType.StatusChanged
                EventType = HistoryEventType.StatusChanged,
                Summary = "Ticket reopened",
                FieldName = "Status",
                OldValue = "Closed",
                NewValue = newStatusName,
                ThreadId = threadId,
                ActorId = actorId,
                ActorName = actorName,
                // 🔥 Rich JSON for Timesheet
                Meta = new
                {
                    action = "reopened",
                    reopenedAt = DateTime.UtcNow,
                    hasThread = threadId.HasValue,
                    threadId = threadId,
                    newState = new { id = newStatusId, name = newStatusName }
                }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // ASSIGNEES
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry AssigneeAdded(
            Guid issueId,
            string assigneeName,
            string department,
            Guid assigneeId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.AssigneeAdded,
                Summary = $"{assigneeName} ({department}) assigned",
                TargetEntityId = assigneeId.ToString(),
                TargetEntityType = "Employee",
                ActorId = actorId,
                ActorName = actorName,
                Meta = new { AssigneeName = assigneeName, Department = department }
            };
        }

        public static TicketHistoryEntry AssigneeRemoved(
            Guid issueId,
            string assigneeName,
            string department,
            Guid assigneeId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.AssigneeRemoved,
                Summary = $"{assigneeName} ({department}) unassigned",
                TargetEntityId = assigneeId.ToString(),
                TargetEntityType = "Employee",
                ActorId = actorId,
                ActorName = actorName,
                Meta = new { AssigneeName = assigneeName, Department = department }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LABELS
        // ─────────────────────────────────────────────────────────────────────
        #region LABELS
        public static TicketHistoryEntry LabelsUpdated(
        Guid? issueId,
        List<HistoryLabelDto> added,
        List<HistoryLabelDto> removed,
        List<HistoryLabelDto> previousState,
        List<HistoryLabelDto> newState,
        Guid actorId,
        string actorName)
        {
            string oldValString = previousState != null && previousState.Any()
                ? string.Join(", ", previousState.Select(l => l.name))
                : "None";

            string newValString = newState != null && newState.Any()
                ? string.Join(", ", newState.Select(l => l.name))
                : "None";

            // 🔥 NEW: Make the summary smart and readable!
            string dynamicSummary = oldValString == "None"
                ? $"Labels set to '{newValString}'"
                : $"Labels changed from '{oldValString}' to '{newValString}'";

            return new TicketHistoryEntry
            {
                IssueId = issueId ?? Guid.Empty,
                EventType = HistoryEventType.TicketUpdated,
                Summary = dynamicSummary,                  // 🔥 Updated
                FieldName = "Labels",
                OldValue = oldValString,
                NewValue = newValString,
                TargetEntityType = "Label",
                ActorId = actorId,
                ActorName = actorName,
                Meta = new { added, removed, previousState, newState }
            };
        }

        //public static TicketHistoryEntry LabelAdded(
        //    Guid? issueId,
        //    //string labelName,
        //    long labelId,
        //    Guid actorId,
        //    string actorName)
        //{
        //    return new TicketHistoryEntry
        //    {
        //        IssueId = issueId ?? Guid.Empty,
        //        EventType = HistoryEventType.LabelAdded,
        //        Summary = $"Label '{labelId}' added",
        //        TargetEntityId = labelId.ToString(),
        //        TargetEntityType = "Label",
        //        ActorId = actorId,
        //        ActorName = actorName
        //    };
        //}

        //public static TicketHistoryEntry LabelRemoved(
        //    Guid issueId,
        //    string labelName,
        //    long labelId,
        //    Guid actorId,
        //    string actorName)
        //{
        //    return new TicketHistoryEntry
        //    {
        //        IssueId = issueId,
        //        EventType = HistoryEventType.LabelRemoved,
        //        Summary = $"Label '{labelName}' removed",
        //        TargetEntityId = labelId.ToString(),
        //        TargetEntityType = "Label",
        //        ActorId = actorId,
        //        ActorName = actorName
        //    };
        //}

        // ─────────────────────────────────────────────────────────────────────
        // WORKSTREAMS (Subtasks)
        // ─────────────────────────────────────────────────────────────────────
        #endregion

        public static TicketHistoryEntry WorkStreamCreated(
            Guid? issueId,
            string assigneeName,
            string streamName,
            string statusName,
            Guid workStreamId,
            Guid actorId,
            string actorName,
            long? threadId = null)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId??Guid.Empty,
                EventType = HistoryEventType.WorkStreamCreated,
                //Summary = $"Subtask created: {streamName} → {assigneeName} ({statusName})",
                //Summary = $"{assigneeName} assigned to this ticket",
                Summary = $"{actorName} assigned {assigneeName} to this ticket",
                WorkStreamId = workStreamId,
                ActorId = actorId,
                ActorName = actorName,
                ThreadId = threadId,
                Meta = new
                {
                    AssigneeName = assigneeName,
                    StreamName = streamName,
                    Status = statusName
                }
            };
        }

        public static TicketHistoryEntry WorkStreamUpdated(
            Guid issueId,
            string assigneeName,
            string oldStatusName,
            string newStatusName,
            int oldPct,
            int newPct,
            Guid workStreamId,
            Guid actorId,
            string actorName)
        {
            var summary = oldStatusName != newStatusName
                ? $"{assigneeName}: {oldStatusName} → {newStatusName} ({newPct}%)"
                : $"{assigneeName}: {oldPct}% → {newPct}%";

            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.WorkStreamUpdated,
                Summary = summary,
                WorkStreamId = workStreamId,
                OldValue = $"{oldStatusName} ({oldPct}%)",
                NewValue = $"{newStatusName} ({newPct}%)",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry WorkStreamCompleted(
            Guid issueId,
            string assigneeName,
            string streamName,
            string NewValue,
            Guid workStreamId,
            Guid actorId,
            string actorName,
            long? threadId,
            string? oldValue
            )
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.WorkStreamCompleted,
                //Summary = $"{assigneeName} completed {streamName}",
                Summary = $"{assigneeName} completed this ticket",
                WorkStreamId = workStreamId,
                FieldName = "Status",
                OldValue = oldValue,
                NewValue = NewValue,
                ActorId = actorId,
                ActorName = actorName,
                ThreadId = threadId,                
            };
        }

        public static TicketHistoryEntry WorkStreamInactive(
            Guid issueId,
            string assigneeName,
            string streamName,
            Guid workStreamId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.WorkStreamInactive,
                Summary = $"{streamName} marked inactive for {assigneeName}",
                WorkStreamId = workStreamId,
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry OverallPctChanged(
            Guid issueId,
            int oldPct,
            int newPct,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.OverallPctChanged,
                Summary = $"Overall progress: {oldPct}% → {newPct}%",
                FieldName = "OverallCompletionPct",
                OldValue = $"{oldPct}%",
                NewValue = $"{newPct}%",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST FAILURES
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry TestFailureReported(
            Guid issueId,
            string testerName,
            string developerName,
            string failureComment,
            int pctDropped,
            Guid workStreamId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.TestFailureReported,
                Summary = $"Test failed: {testerName} → {developerName} (-{pctDropped}%)",
                WorkStreamId = workStreamId,
                ActorId = actorId,
                ActorName = actorName,
                Meta = new
                {
                    TesterName = testerName,
                    DeveloperName = developerName,
                    Comment = failureComment,
                    PercentageDropped = pctDropped
                }
            };
        }

        public static TicketHistoryEntry TestFailureCleared(
            Guid issueId,
            string testerName,
            string developerName,
            Guid workStreamId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.TestFailureCleared,
                Summary = $"Test failure cleared by {testerName}",
                WorkStreamId = workStreamId,
                ActorId = actorId,
                ActorName = actorName,
                Meta = new { TesterName = testerName, DeveloperName = developerName }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // THREADS & ATTACHMENTS
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry ThreadPosted(
            Guid issueId,
            long threadId,
            string actorName,
            Guid actorId)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.ThreadPosted,
                Summary = $"{actorName} posted a comment",
                ThreadId = threadId,
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry AttachmentAdded(
            Guid issueId,
            string fileName,
            Guid actorId,
            string actorName,
            long? threadId = null)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.AttachmentAdded,
                Summary = $"Attachment added: {fileName}",
                ThreadId = threadId,
                ActorId = actorId,
                ActorName = actorName,
                Meta = new { FileName = fileName }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // DAILY PLANS
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry DailyPlanAdded(
            Guid issueId,
            Guid actorId,
            string actorName,
            string date)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.DailyPlanAdded,
                Summary = $"Added to daily plan for {date}",
                ActorId = actorId,
                ActorName = actorName,
                Meta = new { Date = date }
            };
        }

        public static TicketHistoryEntry DailyPlanCompleted(
            Guid issueId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.DailyPlanCompleted,
                Summary = "Marked as completed in daily plan",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry DailyPlanUnchecked(
            Guid issueId,
            string? comment,
            Guid actorId,
            string actorName)
        {
            var summary = string.IsNullOrEmpty(comment)
                ? "Unchecked from daily plan"
                : $"Unchecked from daily plan: {comment}";

            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.DailyPlanUnchecked,
                Summary = summary,
                ActorId = actorId,
                ActorName = actorName,
                Meta = comment != null ? new { Comment = comment } : null
            };
        }
    }
}