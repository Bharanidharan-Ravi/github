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
}
