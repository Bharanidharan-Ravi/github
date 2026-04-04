using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper
{
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
            string oldStatusName,
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
                Meta = new { IsAutoComputed = isAutoComputed }
            };
        }

        public static TicketHistoryEntry TicketClosed(
            Guid issueId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.TicketClosed,
                Summary = "Ticket closed",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry TicketCancelled(
            Guid issueId,
            string? reason,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.TicketCancelled,
                Summary = string.IsNullOrEmpty(reason)
                    ? "Ticket cancelled"
                    : $"Ticket cancelled: {reason}",
                ActorId = actorId,
                ActorName = actorName
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
        // LABELS
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry LabelAdded(
            Guid? issueId,
            //string labelName,
            long labelId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId ?? Guid.Empty,
                EventType = HistoryEventType.LabelAdded,
                Summary = $"Label '{labelId}' added",
                TargetEntityId = labelId.ToString(),
                TargetEntityType = "Label",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        public static TicketHistoryEntry LabelRemoved(
            Guid issueId,
            string labelName,
            long labelId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.LabelRemoved,
                Summary = $"Label '{labelName}' removed",
                TargetEntityId = labelId.ToString(),
                TargetEntityType = "Label",
                ActorId = actorId,
                ActorName = actorName
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // WORKSTREAMS (Subtasks)
        // ─────────────────────────────────────────────────────────────────────

        public static TicketHistoryEntry WorkStreamCreated(
            Guid? issueId,
            string assigneeName,
            string streamName,
            string statusName,
            Guid workStreamId,
            Guid actorId,
            string actorName)
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
            Guid workStreamId,
            Guid actorId,
            string actorName)
        {
            return new TicketHistoryEntry
            {
                IssueId = issueId,
                EventType = HistoryEventType.WorkStreamCompleted,
                //Summary = $"{assigneeName} completed {streamName}",
                Summary = $"{assigneeName} completed this ticket",
                WorkStreamId = workStreamId,
                ActorId = actorId,
                ActorName = actorName
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