using APIGateWay.ModalLayer.nugetmodal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ISyncRepository
    {
        //Task<Dictionary<string, RawSyncResult>> ExecuteAsync(DynamicSyncRequest request);
        Task<SyncResponse> ExecuteAsync(DynamicSyncRequest request);
    }

}
