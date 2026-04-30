using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public interface IRealtimeBroadcaster
    {
        /// <summary>
        /// Fetch rich data for the entity, build a RealtimeMessage, and broadcast.
        /// Never throws — failures are logged and swallowed so the API response
        /// is never blocked or affected by a notification failure.
        /// </summary>
        Task NotifyAsync<TDto>(
            BroadcastEntityConfig<TDto> config,
            string action,
            TDto baseDto) where TDto : class;
    }
}
