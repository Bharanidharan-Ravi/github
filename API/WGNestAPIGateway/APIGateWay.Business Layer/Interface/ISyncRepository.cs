using APIGateWay.ModalLayer.nugetmodal;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ISyncRepository
    {
        //Task<Dictionary<string, RawSyncResult>> ExecuteAsync(DynamicSyncRequest request);
        Task<SyncResponse> ExecuteAsync(DynamicSyncRequest request);
    }

}
