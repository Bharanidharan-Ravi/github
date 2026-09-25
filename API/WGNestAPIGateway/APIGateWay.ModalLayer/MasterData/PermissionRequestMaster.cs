using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    // Maps 1:1 to dbo.PERMISSION_REQUEST (scripts/permission_request_migration.sql).
    [Table("PERMISSION_REQUEST")]
    public class PermissionRequestMaster : IAuditableEntity, IAuditableUser
    {
        [Key]
        [Column("ID")]
        public Guid ID { get; set; }

        [Column("EMPLOYEE_ID")]
        public Guid EMPLOYEE_ID { get; set; }

        [Column("PERMISSION_DATE")]
        public DateTime PERMISSION_DATE { get; set; }

        [Column("DURATION_MINUTES")]
        public int DURATION_MINUTES { get; set; }

        [Column("REMARKS")]
        public string? REMARKS { get; set; }

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

        [Column("ACTUAL_DURATION_MINUTES")]
        public int? ACTUAL_DURATION_MINUTES { get; set; }

        [Column("ACTUAL_DURATION_BY")]
        public Guid? ACTUAL_DURATION_BY { get; set; }

        [Column("ACTUAL_DURATION_DATE")]
        public DateTime? ACTUAL_DURATION_DATE { get; set; }

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
