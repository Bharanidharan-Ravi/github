using APIGateWay.ModalLayer.Helper;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    // One row per logged issue (Issue Logger tab). Linked to its ticket via
    // Issue_Id and to the Thread comment created alongside it via ThreadId.
    public class IssueLog : IAuditableEntity, IAuditableUser
    {
        [Key]
        [IgnoreMapping]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // DB IDENTITY column — this is the "Issue / Point No."
        public int IssueLogId { get; set; }

        public Guid Issue_Id { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Html { get; set; }

        // --------------------------------------------------------
        // 🛡️ AUDIT FIELDS (handled dynamically, same as ThreadMaster)
        // CreatedAt = "Date Raised", UpdatedAt = "Last Updated Date".
        // --------------------------------------------------------
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
