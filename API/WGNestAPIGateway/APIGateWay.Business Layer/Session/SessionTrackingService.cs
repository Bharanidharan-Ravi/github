using APIGateWay.DomainLayer.CommonSevice;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Session
{
    public class SessionTrackingService : ISessionTrackingService
    {
        private readonly APIGateWayCommonService _service;

        public SessionTrackingService(
            APIGateWayCommonService service)
        {
            _service = service;
        }

        public async Task SignalRConnectedAsync(
            Guid sessionId,
            Guid userId,
            string connectionId,
            int repoCount)
        {
            await _service.ExecuteReturnAsync(
                "USP_SignalRConnected",
                new[]
                {
                new SqlParameter("@SessionId", sessionId),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@ConnectionId", connectionId),
                new SqlParameter("@RepoCount", repoCount)
                });
        }

        public async Task UpdateConnectionAsync(
            Guid sessionId,
            string connectionId)
        {
            await _service.ExecuteReturnAsync(
                "USP_UpdateSignalRConnection",
                new[]
                {
                new SqlParameter("@SessionId", sessionId),
                new SqlParameter("@ConnectionId", connectionId)
                });
        }

        public async Task SignalRDisconnectedAsync(
            string connectionId,
            string reason)
        {
            await _service.ExecuteReturnAsync(
                "USP_SignalRDisconnected",
                new[]
                {
                new SqlParameter("@ConnectionId", connectionId),
                new SqlParameter("@Reason", reason)
                });
        }
    }

}