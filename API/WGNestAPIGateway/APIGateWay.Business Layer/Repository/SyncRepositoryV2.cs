using APIGateWay.BusinessLayer.Configuration;
using APIGateWay.BusinessLayer.Helper;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.nugerModalV2;
using APIGateWay.ModalLayer.nugetmodal;
using System;

namespace APIGateWay.BusinessLayer.Repository
{
    public class SyncRepositoryV2 : ISyncRepositoryV2
    {
        private readonly ISyncExecutionService _executionService;

        public SyncRepositoryV2(ISyncExecutionService executionService)
        {
            _executionService = executionService;
        }

        public async Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request)
        {
            var tasks = new Dictionary<string, Task<RawSyncResult>>();

            // -------- Build execution plan (config-driven) --------
            foreach (var key in request.ConfigKeys)
            {
                if (!SyncRepositoryConfigStore.Configs.TryGetValue(key, out var cfg))
                {
                    tasks[key] = Task.FromResult(new RawSyncResult
                    {
                        Ok = false,
                        ErrorCode = "INVALID_CONFIG_KEY",
                        ErrorMessage = "Invalid setup. Contact admin.",
                        Retryable = false,
                        Source = "Repository"
                    });
                    continue;
                }

                request.Timestamps.TryGetValue(key, out var lastSync);
                request.Params.TryGetValue(key, out var param);

                tasks[key] = cfg.SourceType switch
                {
                    SyncSourceType.Local =>
                        ExecuteLocal(cfg, lastSync, param),

                    SyncSourceType.Remote =>
                        _executionService.ExecuteRemoteAsync(
                            cfg.Endpoint,
                            lastSync,
                            param,
                            cfg.SourceName
                        ),

                    _ => Task.FromResult(new RawSyncResult
                    {
                        Ok = false,
                        ErrorCode = "INVALID_SOURCE_TYPE",
                        ErrorMessage = "Config error. Contact admin.",
                        Retryable = false,
                        Source = "Repository"
                    })
                };
            }

            // -------- Execute in parallel --------
            await Task.WhenAll(tasks.Values);

            // -------- Build v2 compact response --------
            var response = new SyncResponseV2
            {
                Rid = Guid.NewGuid().ToString(),
                St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            foreach (var kv in tasks)
            {
                var raw = kv.Value.Result;

                if (raw.Ok)
                {
                    response.Res[kv.Key] = new SyncResultV2
                    {
                        Ok = true,
                        Data = raw.Data
                    };
                }
                else
                {
                    response.Res[kv.Key] = new SyncResultV2
                    {
                        Ok = false,
                        Err = new SyncErrorV2
                        {
                            C = raw.ErrorCode,
                            M = raw.ErrorMessage,
                            R = raw.Retryable
                        }
                    };
                }
            }

            return response;
        }

        // -------- Local execution (generic, config-driven) --------
        private Task<RawSyncResult> ExecuteLocal(
            SyncRepositoryConfig cfg,
            DateTimeOffset? lastSync,
            Dictionary<string, string> param)
        {
            return (Task<RawSyncResult>)typeof(ISyncExecutionService)
                .GetMethod(nameof(ISyncExecutionService.ExecuteLocalAsync))
                .MakeGenericMethod(cfg.EntityType)
                .Invoke(_executionService, new object[]
                {
                null,                    // databaseName (handled internally)
                cfg.StoredProcedure,
                lastSync,
                param,
                cfg.SourceName
                });
        }
    }

}
