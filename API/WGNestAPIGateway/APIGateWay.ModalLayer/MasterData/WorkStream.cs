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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // matches newsequentialid()
        public Guid Id { get; set; }

        // FK → TicketMaster.Issue_Id  (same as ThreadMaster.TicketId)
        public Guid? IssueId { get; set; }

        [MaxLength(20)]
        public string? StreamName { get; set; }

        // 1 = Active (default on create)
        public int? StreamStatus { get; set; } = 1;

        // FK → Employee/User who is responsible for this stream
        public Guid? ResourceId { get; set; }

        public decimal? CompletionPct { get; set; } = 0;

        public DateTime? TargetDate { get; set; }

        // ── IAuditableEntity ─────────────────────────────────────────────────
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ── IAuditableUser ───────────────────────────────────────────────────
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    public class WorkStreamContext
    {
        // ── Required ─────────────────────────────────────────────────────────
        public Guid IssueId { get; set; }

        // ── Optional — auto-resolved from EMPLOYEEMASTER.Team if null ────────
        // Pass null  → StreamName = current user's Team (e.g. "Development")
        // Pass value → use that value directly (owner forcing a stage)
        public string? StreamName { get; set; }

        // ── Optional — auto-resolved from ResourceId rules if null ───────────
        // Pass null  → service applies _selfResourceStreams rule
        // Pass value → used only when StreamName is NOT a self-resource stream
        public Guid? ResourceId { get; set; }

        // ── WorkStream data ───────────────────────────────────────────────────
        public decimal? CompletionPct { get; set; }
        public DateTime? TargetDate { get; set; }
    }

    // Returned by UpsertWorkStreamAsync so callers can update TicketMaster
    public class WorkStreamResult
    {
        public Guid Id { get; set; }
        public string? StreamName { get; set; }
        public Guid? ResourceId { get; set; }
        public bool WasInserted { get; set; }  // true = new row, false = updated existing
    }
}
