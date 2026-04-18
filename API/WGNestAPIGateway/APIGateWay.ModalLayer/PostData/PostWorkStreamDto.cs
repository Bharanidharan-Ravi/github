using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    // Input for POST /api/workstream (individual stream post)
    //public class PostWorkStreamDto
    //{
    //    public Guid IssueId { get; set; }
    //    public Guid? ResourceId { get; set; }
    //    public Guid? NextAssigneeId { get; set; }
    //    public int? NextAssigneeStreamId { get; set; }
    //    public bool AssignOnly { get; set; } = false;
    //    // StreamName from UI dropdown (user picks their stage explicitly)
    //    //public string StreamName { get; set; } = string.Empty;

    //    // StatusId from Status_Master — sent from UI
    //    // e.g. user picks "In Development" → UI sends 5
    //    public int? StreamStatus { get; set; }
    //    // Toggle button:
    //    //   true  = link last thread of this user for this ticket
    //    //   false = create new ThreadMaster row from Comment
    //    public bool UseLastThread { get; set; } = false;
    //    public string? Comment { get; set; }
    //    public decimal? CompletionPct { get; set; }
    //    public DateTime? TargetDate { get; set; }
    //    public long? ParentThreadId { get; set; }
    //    public bool ReportTestFailure { get; set; } = false;
    //    public string? TestFailureComment { get; set; }
    //    public bool ClearTestFailure { get; set; } = false;
    //    public Guid? TargetDeveloperResourceId { get; set; }
    //    public decimal? PercentageDrop { get; set; }
    //    public TempReturn? temp { get; set; }
    //    public string? StreamName { get; set; }
    //    public DateTime? From_Time { get; set; }
    //    public DateTime? To_Time { get; set; }
    //    public string? Hours { get; set; }
    //}

    public class PostWorkStreamDto
    {
        public Guid IssueId { get; set; }
        public Guid? ResourceId { get; set; }
        public int? handsoffId { get; set; }
        public bool AssignOnly { get; set; } = false;

        // ── BEFORE (single assignee) ───────────────────────────────────────
        // public Guid? NextAssigneeId       { get; set; }
        // public int?  NextAssigneeStreamId { get; set; }

        // ── AFTER (multiple assignees) ────────────────────────────────────
        // Each entry = one person assigned to one specific stage
        // e.g. [{ Id: "anbu-guid", StreamId: 5 }, { Id: "danu-guid", StreamId: 7 }]
        public List<NextAssigneeDto>? NextAssignees { get; set; }

        public string StreamName { get; set; } = string.Empty;
        public int? StreamStatus { get; set; }
        public List<int>? ResolvedHandoffIds { get; set; }
        public bool? UseLastThread { get; set; } = null;
        public string? CommentText { get; set; }
        public TempReturn? temp { get; set; }
        public decimal? CompletionPct { get; set; }
        public DateTime? TargetDate { get; set; }
        public long? ParentThreadId { get; set; }
        public DateTime? From_Time { get; set; }
        public DateTime? To_Time { get; set; }
        public string? Hours { get; set; }
        public bool IsReopenRequest { get; set; }
        public int? ReopenCount { get; set; }
        public Guid? ReopenedBy { get; set; }
        public bool ReportTestFailure { get; set; } = false;
        public string? TestFailureComment { get; set; }
        public bool ClearTestFailure { get; set; } = false;
        public Guid? TargetDeveloperResourceId { get; set; }
        public Guid? WorkStreamId { get; set; }
        public decimal? PercentageDrop { get; set; }
        public decimal? TicketOverallPercentage { get; set; }
        public string? TicketStatusSummary { get; set; }

        // Set this to true from the UI if the user is ONLY submitting 
        // the overall progress and NOT updating their subtask/comments.
        public bool IsTicketProgressOnly { get; set; }
    }

    public class NextAssigneeDto
    {
        // The person being assigned
        public Guid Id { get; set; }

        // Which stage to assign them to (FK → Status_Master.Status_Id)
        // e.g. 5=InDevelopment, 7=UnitTesting, 8=FunctionalTesting
        public int StreamId { get; set; }

        // Optional deadline for this specific assignment
        public DateTime? TargetDate { get; set; }
    }

}
