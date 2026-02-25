using APIGateWay.ModalLayer.nugerModalV2;
using APIGateWay.ModalLayer.nugetmodal;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ISyncRepositoryV2
    {
        Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request);
    }
}
