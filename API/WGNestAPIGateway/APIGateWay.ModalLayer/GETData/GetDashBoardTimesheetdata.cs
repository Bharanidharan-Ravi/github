using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class DashBoardTimeSheetData
    {
        [Key]
        public int ThreadId { get; set; }
        public Guid Issue_Id { get; set; }

        public string? EmployeeName { get; set; }

        public string? Repository_Name { get; set; }

        public string? Project_Name { get; set; }

        public string? TicketNo { get; set; }

        public string? TicketName { get; set; }

        public string? EstimatedHours { get; set; }

        public string? ConsumeTime { get; set; }

        public string? Comment { get; set; }


        public string? StartTime { get; set; }

        public string? EndTime { get; set; }


        public string? total { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
