using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class TicketProgressLogDto
    {
        [Key]
        public Guid LogId { get; set; }
        public Guid Issue_Id { get; set; }
        public decimal? Percentage { get; set; }
        public string? StatusSummary { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid Assignee_Id { get; set; }

        // The joined field from the SP
        public string AssigneeName { get; set; }
    }
}
