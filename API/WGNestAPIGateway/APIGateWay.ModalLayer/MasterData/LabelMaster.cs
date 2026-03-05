// ─────────────────────────────────────────────────────────────────────────────
// Namespace : APIGateWay.ModalLayer.MasterData
// File      : LabelMaster.cs
//
// Maps to DB table: [dbo].[LABELMASTER]
// PK: int IDENTITY — NOT Guid (different from ProjectMaster, TicketMaster)
// Audit: string fields Created_By/Updated_By — NOT IAuditableUser interface
//        Set manually in service from ILoginContextService.userName
// No Repo_Id — labels are global master data, not repo-scoped
// ─────────────────────────────────────────────────────────────────────────────
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("LABELMASTER")]
    public class LabelMaster
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? Created_On { get; set; }

        [MaxLength(100)]
        public string? Created_By { get; set; }

        public DateTime? Updated_On { get; set; }

        [MaxLength(100)]
        public string? Updated_By { get; set; }

        [MaxLength(100)]
        public string? Status { get; set; }

        [MaxLength(20)]
        public string? Color { get; set; }
    }
}