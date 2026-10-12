using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IVersionRepo
    {
        Task<string> GetAppVersion();
        Task PublishVersionAsync();
    }
}
