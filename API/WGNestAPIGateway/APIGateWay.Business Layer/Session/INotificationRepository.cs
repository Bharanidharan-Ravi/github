using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Session
{
    public interface INotificationRepository
    {
        Task<Guid> CreateAsync(CreateNotificationRequest request);
        Task<int> GetUnreadCountAsync(
       Guid userId);
        Task EnsureUserStateAsync(
        Guid userId);
        Task<List<NotificationListResponse>> GetNotificationsAsync(Guid userId);
        Task MarkSeenAsync(Guid userId);
    }
}
