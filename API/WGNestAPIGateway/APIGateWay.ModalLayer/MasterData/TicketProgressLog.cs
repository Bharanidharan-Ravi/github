using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class TicketProgressLog
    {
        [Key]
        public Guid LogId { get; set; } = Guid.NewGuid();
        public Guid Issue_Id { get; set; } // FK to TicketMaster
        public Guid Assignee_Id { get; set; } // Who wrote this update

        public decimal? Percentage { get; set; } // The manual percentage
        public string? StatusSummary { get; set; } // "3 points are completed..."

        public bool IsActive { get; set; } // True for the CURRENT status, false for history
        public DateTime CreatedAt { get; set; }
    }
}
