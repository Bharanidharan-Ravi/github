using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.DTOs
{
    public class NotificationListResponse
    {
        public Guid NotificationId { get; set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public string EntityType { get; set; }

        public string EntityId { get; set; }
        public Guid? ActorId { get; set; }
        public string ActorName { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
