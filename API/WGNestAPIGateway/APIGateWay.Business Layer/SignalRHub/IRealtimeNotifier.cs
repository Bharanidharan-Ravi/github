using APIGateWay.ModalLayer.Hub;
using System;

namespace APIGateWay.BusinessLayer.SignalRHub
{
    public interface IRealtimeNotifier
    {
        Task BroadcastAsync(RealtimeMessage message);
    }
}