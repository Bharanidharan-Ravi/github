using APIGateWay.ModalLayer.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper.Events.Interface
{
    public interface IEventCenter
    {
        Task PublishAsync(
            EventRequest request,
            bool notify = true,
            bool signalR = true);
    }
}
