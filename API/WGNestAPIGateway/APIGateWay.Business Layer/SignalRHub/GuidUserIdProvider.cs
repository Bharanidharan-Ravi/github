using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace APIGateWay.Business_Layer.SignalRHub
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
