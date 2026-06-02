using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper.Events.Interface
{
    public interface IEventContextProvider
    {
        Dictionary<string, string> GetContextValues();
    }
}
