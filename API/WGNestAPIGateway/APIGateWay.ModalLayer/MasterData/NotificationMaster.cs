using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    public class CreateNotificationRequest
    {
        public string EventType { get; set; }

        public string EntityType { get; set; }
        public string? EntityId { get; set; }

        public Guid? RepositoryId { get; set; }

        public string Title { get; set; }
        public string Message { get; set; }

        public Guid? ActorId { get; set; }
        public string ActorName { get; set; }

        public List<NotificationAudienceDto> Audiences { get; set; }
    }

    public class NotificationAudienceDto
    {
        public string AudienceType { get; set; }
        public string AudienceValue { get; set; }
    }
    public class NotificationMaster : IHasCreatedAt
    {
        [Key]
        public Guid NotificationId { get; set; }
        public string EventType { get; set; }

        public string EntityType { get; set; }
        public Guid EntityId { get; set; }

        public Guid? RepositoryId { get; set; }

        public string Title { get; set; }
        public string Message { get; set; }

        public Guid? ActorId { get; set; }
        public string ActorName { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
    public class NotificationAudience
    {
        [Key]
        public Guid AudienceId { get; set; }

        public Guid NotificationId { get; set; }

        public string AudienceType { get; set; }

        public string AudienceValue { get; set; }
    }
}
