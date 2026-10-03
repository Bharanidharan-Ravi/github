using APIGateWay.BusinessLayer.Auth;
using APIGateWay.ModalLayer.nugerModalV2;
using APIGateWay.ModalLayer.nugetmodal;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ISyncRepositoryV2
    {
        Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request);
        Task<SyncResponseV2> ExecuteUnitsAsync(List<SyncExecutionUnit> units);

        /// <summary>Role-checked run of config keys: the whole /sync/v2 flow, reusable by other endpoints.</summary>
        Task<SyncResponseV2> RunAsync(DynamicSyncRequest request);
        Task<SyncResponseV2> RunAsync(params string[] configKeys);
    }
}
