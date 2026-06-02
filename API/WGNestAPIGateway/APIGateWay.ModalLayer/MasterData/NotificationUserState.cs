using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.ModalLayer.MasterData
{
    public class NotificationUserState : IHasUpdatedAt,IHasLastSeen
    {
        [Key]
        public Guid UserId { get; set; }

        public DateTime? LastSeenAt { get; set; }

        public Guid? LastNotificationId { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
