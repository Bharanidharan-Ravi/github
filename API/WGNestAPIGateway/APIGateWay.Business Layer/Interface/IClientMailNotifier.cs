using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IClientMailNotifier
    {
        Task NotifyThreadToClientAsync(Guid issueId, string threadHtml, string postedBy, bool isUpdate);
    }
}



