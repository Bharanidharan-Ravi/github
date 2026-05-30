using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Session
{
    public interface ISessionTrackingService
    {
        Task SignalRConnectedAsync(
            Guid sessionId,
            Guid userId,
            string connectionId,
            int repoCount);

        Task SignalRDisconnectedAsync(
            string connectionId,
            string reason);

        Task UpdateConnectionAsync(
            Guid sessionId,
            string connectionId);
    }
}
