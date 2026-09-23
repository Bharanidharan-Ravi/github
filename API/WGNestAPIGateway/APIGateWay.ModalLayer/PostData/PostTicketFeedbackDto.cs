using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.PostData
{
    public class PostTicketFeedbackDto
    {
        public Guid TicketId { get; set; }
        public Guid RepoId { get; set; }
        public List<Guid> UserIds { get; set; } = new();
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
