using APIGateWay.ModalLayer.nugerModalV2;
using APIGateWay.ModalLayer.nugetmodal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ISyncRepositoryV2
    {
        Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request);
    }
}
