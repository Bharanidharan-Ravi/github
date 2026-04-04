using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    [Table("TicketHistory")]
    public class TicketHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [Required]
        public Guid IssueId { get; set; }

        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Summary { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FieldName { get; set; }

        [MaxLength(500)]
        public string? OldValue { get; set; }

        [MaxLength(500)]
        public string? NewValue { get; set; }

        [Required]
        public Guid ActorId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ActorName { get; set; } = string.Empty;

        public Guid? WorkStreamId { get; set; }
        public long? ThreadId { get; set; }

        [MaxLength(100)]
        public string? TargetEntityId { get; set; }

        [MaxLength(50)]
        public string? TargetEntityType { get; set; }

        public string? MetaJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}


