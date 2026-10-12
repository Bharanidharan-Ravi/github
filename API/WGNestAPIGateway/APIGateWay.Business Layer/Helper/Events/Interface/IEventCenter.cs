using APIGateWay.ModalLayer.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Helper.Events.Interface
{
    public interface IEventCenter
    {
        Task<T?> PublishAsync<T>(
            EventRequest request,
            bool notify = true,
            bool signalR = true);
    }
}
