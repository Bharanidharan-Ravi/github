using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

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
        public int? ThreadId { get; set; }

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
        public int? ParentThreadId { get; set; }
    }

    // Returned by UpsertWorkStreamAsync to callers
    public class WorkStreamResult
    {
        public Guid StreamId { get; set; }
        public string? StreamName { get; set; }
        public string? StatusName { get; set; }  // e.g. "In Development"
        public Guid ResourceId { get; set; }
        public int? StreamStatus { get; set; }  // FK int → Status_Master.Id
        public long? ParentThreadId { get; set; }
        public long? ThreadId { get; set; }
        public bool WasInserted { get; set; }
    }

    // Response for PostWorkStreamAsync
    public class PostWorkStreamResponse
    {
        public Guid WorkStreamId { get; set; }
        public long? ThreadId { get; set; }
        public long? ParentThreadId { get; set; }
        public string StreamName { get; set; } = string.Empty;
        public int StreamStatus { get; set; }
        public string? StatusName { get; set; }  // human-readable from Status_Master
        public bool ThreadCreated { get; set; }
        public bool TicketCompleted { get; set; }
        public int TicketStatusId { get; set; }
        public string? TicketStatusName { get; set; }
        public decimal TicketOverallPct { get; set; }
        public int? TotalSubtasks { get; set; }
        public int CompletedSubtasks { get; set; }
        public int ActiveSubtasks { get; set; }
    }
}
