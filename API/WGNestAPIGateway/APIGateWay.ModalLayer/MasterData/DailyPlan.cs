using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("DailyPlans")]
    public class DailyPlan : IAuditableEntity
    {
        [Key]
        public int Id { get; set; }

        [Required] public Guid UserId { get; set; }
        [Required] public Guid TicketId { get; set; }
        public string? ProjKey { get; set; }
        public string? RepoKey { get; set; }

        [Required] public DateTime PlannedDate { get; set; }  // DATE only, no time

        // 1=Active  2=Success  3=Failed  4=Unchecked
        public int Status { get; set; } = 1;

        [MaxLength(500)] public string? UncheckComment { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public static class DailyPlanStatus
    {
        public const int Active = 1;
        public const int Success = 2;
        public const int Failed = 3;
        public const int Unchecked = 4;
    }
}
