using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    public class GuidUserIdProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            return connection.User?
                .FindFirst(ClaimTypes.NameIdentifier)?
                .Value;
        }
    }
}
