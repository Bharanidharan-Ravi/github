using APIGateWay.ModalLayer.Hub;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    public interface IRealtimeNotifier
    {
        Task BroadcastAsync(RealtimeMessage message);
    }
}