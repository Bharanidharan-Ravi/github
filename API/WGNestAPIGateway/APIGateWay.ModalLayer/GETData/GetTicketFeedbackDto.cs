using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.GETData
{
    public class GetTicketFeedbackDto
    {
        public Guid FeedbackId { get; set; }
        public Guid RepoId { get; set; }
        public Guid TicketId { get; set; }
        public Guid UserId { get; set; }
        public string? EmployeeName { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
