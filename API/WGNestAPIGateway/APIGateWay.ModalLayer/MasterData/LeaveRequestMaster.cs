using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    // Maps 1:1 to dbo.LEAVE_REQUEST (scripts/leave_request_migration.sql).
    [Table("LEAVE_REQUEST")]
    public class LeaveRequestMaster : IAuditableEntity, IAuditableUser
    {
        [Key]
        [Column("ID")]
        public Guid ID { get; set; }

        [Column("EMPLOYEE_ID")]
        public Guid EMPLOYEE_ID { get; set; }

        [Column("LEAVE_FROM")]
        public DateTime LEAVE_FROM { get; set; }

        [Column("LEAVE_TO")]
        public DateTime LEAVE_TO { get; set; }

        [Column("LEAVE_TYPE_ID")]
        public int LEAVE_TYPE_ID { get; set; }

        [Column("NO_OF_LEAVE_DAYS")]
        public int NO_OF_LEAVE_DAYS { get; set; }

        [Column("COMMENTS")]
        public string? COMMENTS { get; set; }

        [Column("STATUS")]
        public string STATUS { get; set; }

        [Column("REQUESTED_DATE")]
        public DateTime REQUESTED_DATE { get; set; }

        [Column("APPROVED_BY")]
        public Guid? APPROVED_BY { get; set; }

        [Column("APPROVED_DATE")]
        public DateTime? APPROVED_DATE { get; set; }

        [Column("REJECT_REASON")]
        public string? REJECT_REASON { get; set; }

        [Column("REJECTED_BY")]
        public Guid? REJECTED_BY { get; set; }

        [Column("REJECTED_DATE")]
        public DateTime? REJECTED_DATE { get; set; }

        // ── Audit (auto-populated in IST by APIGatewayDBContext.SaveChangesAsync) ──
        [Column("CREATED_BY")]
        public Guid? CreatedBy { get; set; }

        [Column("CREATED_DATE")]
        public DateTime? CreatedAt { get; set; }

        [Column("UPDATED_BY")]
        public Guid? UpdatedBy { get; set; }

        [Column("UPDATED_DATE")]
        public DateTime? UpdatedAt { get; set; }
    }
}
