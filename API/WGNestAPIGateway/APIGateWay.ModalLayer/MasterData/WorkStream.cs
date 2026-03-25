using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;
using APIGateWay.ModalLayer.PostData;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("WorkStreams")]
    public class WorkStream : IAuditableEntity, IAuditableUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid StreamId { get; set; }

        // FK → TicketMaster.Issue_Id
        public Guid? IssueId { get; set; }

        // FK → EMPLOYEEMASTER.EmployeeID
        public Guid? ResourceId { get; set; }

        // Department name — auto-resolved from EMPLOYEEMASTER.Team
        [MaxLength(20)]
        public string? StreamName { get; set; }

        // FK → Status_Master.Id  (was plain int, now references status table)
        // e.g. 5 = InDevelopment, 18 = Closed, 20 = Inactive
        public int? StreamStatus { get; set; }

        public decimal? CompletionPct { get; set; }
        public DateTime? TargetDate { get; set; }

        // FK → ThreadMaster.ThreadId (BIGINT from sequence)
        // The current/latest progress thread for this subtask
        public long? ThreadId { get; set; }

        // FK → ThreadMaster.ThreadId
        // The scope/planning thread that defines what this subtask covers
        // NULL = ticket itself is the parent (no planning thread yet)
        public long? ParentThreadId { get; set; }

        // ── IAuditableEntity ──────────────────────────────────────────────────
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ── IAuditableUser ────────────────────────────────────────────────────
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }

        // Navigation (optional — for joins without EF Include)
        [ForeignKey("StreamStatus")]
        public StatusMaster? Status { get; set; }
        public bool BlockedByTestFailure { get; set; } = false;
        public string? BlockedReason { get; set; }
        public DateTime? BlockedAt { get; set; }
        public Guid? BlockedByResourceId { get; set; }

    }

    // Input for UpsertWorkStreamAsync
    public class WorkStreamContext
    {
        public Guid? IssueId { get; set; }
        public Guid ResourceId { get; set; }

        // Pass StatusId constant — e.g. StatusId.InDevelopment (5)
        public int? StreamStatus { get; set; }

        public decimal? CompletionPct { get; set; }
        public DateTime? TargetDate { get; set; }
        public long? ParentThreadId { get; set; }
    }

    // Returned by UpsertWorkStreamAsync to callers
    public class WorkStreamResult
    {
        public Guid StreamId { get; set; }
        public string? StreamName { get; set; }
        public string? StatusName { get; set; }
        public Guid ResourceId { get; set; }
        public int? StreamStatus { get; set; }
        public long? ParentThreadId { get; set; }
        public long? ThreadId { get; set; }
        public bool WasInserted { get; set; }
        public bool IsBlocked { get; set; }
        public string? BlockedReason { get; set; }

        // ← Returned so the Business Layer caller can broadcast without
        //   making another DB round trip
        public TicketStatusResult? TicketStatus { get; set; }
    }

    // Response for PostWorkStreamAsync
    public class PostWorkStreamResponse
    {
        public Guid WorkStreamId { get; set; }
        public Guid ResourceId { get; set; }
        public long? ThreadId { get; set; }
        public long? ParentThreadId { get; set; }
        public string StreamName { get; set; } = string.Empty;

        public int? StreamStatus { get; set; }
        public string? StatusName { get; set; }  // human-readable from Status_Master
        public bool IsBlocked { get; set; }
        public string? BlockedReason { get; set; }
        public bool ThreadCreated { get; set; }
        public bool TicketCompleted { get; set; }
        public int TicketStatusId { get; set; }
        public string? TicketStatusName { get; set; }
        public decimal TicketOverallPct { get; set; }
        public int? TotalSubtasks { get; set; }
        public int CompletedSubtasks { get; set; }
        public int ActiveSubtasks { get; set; }
        public bool DeveloperBlocked { get; set; }
        public string BlockSummary { get; set; }
        public bool IsTerminal { get; set; }
        public string RepoKey { get; set; }
        public Guid IssueId { get; set; }
        public decimal? CompletionPct { get; set; }
        public object? BroadcastPayload { get; set; }
        // Tells the UI the block was cleared (dev can now mark DevCompleted)
        public bool DeveloperUnblocked { get; set; }
    }
}



// =============================================================================
// WHAT UI SENDS FOR EACH SCENARIO
// =============================================================================
 
// SCENARIO 1: Developer posts normal progress
// POST /api/workstream
// {
//   "IssueId":      "ticket-guid",
//   "StreamName":   "Development",
//   "StreamStatus": 5,              // InDevelopment
//   "CompletionPct": 80,
//   "UseLastThread": false,
//   "Comment":      "Fixed auth module, 80% done"
// }
 
// SCENARIO 2: Developer marks DevCompleted (normal path, no block)
// {
//   "StreamStatus": 6,              // DevelopmentCompleted
//   "CompletionPct": 100,
//   ...
// }
 
// SCENARIO 3: Tester reports failure (blocks developer A)
// {
//   "StreamStatus":            8,   // FunctionalTesting (tester's own status)
//   "CompletionPct":           60,  // tester's own progress
//   "ReportTestFailure":       true,
//   "TestFailureComment":      "Login button throws 500 on wrong password",
//   "TargetDeveloperResourceId": "developer-A-guid",  // null = block all devs
//   "PercentageDrop":          30,  // drop dev by 30%
//   ...
// }
 
// SCENARIO 4: Tester clears failure (unblocks developer)
// {
//   "StreamStatus":            8,   // FunctionalTesting (tester's own status)
//   "CompletionPct":           90,  // tester's own progress
//   "ClearTestFailure":        true,
//   "TargetDeveloperResourceId": "developer-A-guid",
//   ...
// }
 
// SCENARIO 5: Developer marks DevCompleted after being unblocked
// {
//   "StreamStatus": 6,   // DevelopmentCompleted — now allowed since block cleared
//   "CompletionPct": 100,
//   ...
// }
// If block NOT cleared → throws 400: "Cannot mark DevelopmentCompleted. Testing failed..."