//using APIGateWay.Business_Layer.Configuration;
//using APIGateWay.DomainLayer.Interface;
//using APIGateWay.ModalLayer.nugerModalV2;
//using APIGateWay.ModalLayer.nugetmodal;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace APIGateWay.DomainLayer.Service
//{
//    public class SyncServiceV2 : ISyncServiceV2
//    {
//        private readonly ISyncExecutionService _executionService;

//        public SyncServiceV2(ISyncExecutionService executionService)
//        {
//            _executionService = executionService;
//        }

//        public async Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request)
//        {
//            var tasks = new Dictionary<string, Task<RawSyncResult>>();

//            foreach (var key in request.ConfigKeys)
//            {
//                if (!SyncRepositoryConfigStore.Configs.TryGetValue(key, out var cfg))
//                    continue;

//                request.Timestamps.TryGetValue(key, out var lastSync);
//                request.Params.TryGetValue(key, out var param);

//                tasks[key] = cfg.SourceType switch
//                {
//                    SyncSourceType.Local =>
//                        ExecuteLocal(cfg, lastSync, param),

//                    SyncSourceType.Remote =>
//                        _executionService.ExecuteRemoteAsync(
//                            cfg.Endpoint,
//                            lastSync,
//                            param,
//                            cfg.SourceName
//                        ),

//                    _ => Task.FromResult(new RawSyncResult
//                    {
//                        Ok = false,
//                        ErrorCode = "INVALID_SOURCE",
//                        ErrorMessage = "Invalid source type",
//                        Retryable = false,
//                        Source = "SyncV2"
//                    })
//                };
//            }

//            await Task.WhenAll(tasks.Values);

//            var response = new SyncResponseV2
//            {
//                Rid = Guid.NewGuid().ToString(),
//                St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
//            };

//            foreach (var kv in tasks)
//            {
//                var raw = kv.Value.Result;

//                if (raw.Ok)
//                {
//                    response.Res[kv.Key] = new SyncResultV2
//                    {
//                        Ok = true,
//                        Data = raw.Data
//                    };
//                }
//                else
//                {
//                    response.Res[kv.Key] = new SyncResultV2
//                    {
//                        Ok = false,
//                        Err = new SyncErrorV2
//                        {
//                            C = raw.ErrorCode,
//                            M = raw.ErrorMessage,
//                            R = raw.Retryable
//                        }
//                    };
//                }
//            }

//            return response;
//        }

//        private Task<RawSyncResult> ExecuteLocal(
//            SyncRepositoryConfig cfg,
//            DateTimeOffset? lastSync,
//            Dictionary<string, string> param)
//        {
//            return (Task<RawSyncResult>)typeof(ISyncExecutionService)
//                .GetMethod(nameof(ISyncExecutionService.ExecuteLocalAsync))
//                .MakeGenericMethod(cfg.EntityType)
//                .Invoke(_executionService, new object[]
//                {
//                null,
//                cfg.StoredProcedure,
//                lastSync,
//                param,
//                cfg.SourceName
//                });
//        }
//    }
//}
