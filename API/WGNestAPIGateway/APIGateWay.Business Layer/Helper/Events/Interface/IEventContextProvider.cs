using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Helper.Events.Interface
{
    public interface IEventContextProvider
    {
        Dictionary<string, string> GetContextValues();
    }
}
