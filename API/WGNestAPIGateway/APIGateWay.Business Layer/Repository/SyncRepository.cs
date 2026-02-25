
using APIGateWay.BusinessLayer.Configuration;
using APIGateWay.BusinessLayer.Helper;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.nugetmodal;
using System;
using System.Text.Json;

namespace APIGateWay.BusinessLayer.Repository
{
    public class SyncRepository : ISyncRepository
    {
        private readonly ISyncExecutionService _executionService;

        public SyncRepository(ISyncExecutionService executionService)
        {
            _executionService = executionService;
        }

        public async Task<SyncResponse> ExecuteAsync(DynamicSyncRequest request)
        {
            var rawResults = new Dictionary<string, RawSyncResult>();
            var tasks = new Dictionary<string, Task<RawSyncResult>>();

            foreach (var key in request.ConfigKeys)
            {
                if (!SyncRepositoryConfigStore.Configs.TryGetValue(key, out var config))
                {
                    rawResults[key] = new RawSyncResult
                    {
                        Ok = false,
                        ErrorCode = "INVALID_CONFIG_KEY",
                        ErrorMessage = $"Unknown sync key: {key}",
                        Retryable = false,
                        Source = "Repository"
                    };
                    continue;
                }

                request.Timestamps.TryGetValue(key, out var lastSync);
                request.Params.TryGetValue(key, out var param);

                tasks[key] = ExecuteByConfig(config, lastSync, param);
            }

            await Task.WhenAll(tasks.Values);

            foreach (var t in tasks)
                rawResults[t.Key] = t.Value.Result;

            // -------- AGGREGATION HAPPENS HERE --------
            var results = new Dictionary<string, SyncModuleResult>();

            foreach (var kv in rawResults)
            {
                var key = kv.Key;
                var raw = kv.Value;
                var cfg = SyncRepositoryConfigStore.Configs[key];

                if (raw.Ok)
                {
                    var list = raw.Data as IEnumerable<object>;

                    results[key] = new SyncModuleResult
                    {
                        Ok = true,
                        Type = cfg.Type,
                        Strategy = cfg.Strategy,
                        IdKey = cfg.IdKey,
                        Data = raw.Data,
                        Meta = new SyncMeta
                        {
                            //Count = list?.Count() ?? 0,
                            Count = raw.Data is JsonElement je && je.ValueKind == JsonValueKind.Array
    ? je.GetArrayLength()
    : 0,
                            Delta = cfg.DeltaEnabled && request.Timestamps.ContainsKey(key),
                            LastSync = DateTimeOffset.UtcNow
                        }
                    };
                }
                else
                {
                    results[key] = new SyncModuleResult
                    {
                        Ok = false,
                        Error = new SyncError
                        {
                            Code = raw.ErrorCode,
                            Message = raw.ErrorMessage,
                            Retryable = raw.Retryable,
                            Severity = "error",
                            Source = raw.Source
                        }
                    };
                }
            }

            return new SyncResponse
            {
                Ok = true,
                RequestId = Guid.NewGuid().ToString(),
                ServerTime = DateTimeOffset.UtcNow,
                Mode = request.ConfigKeys.Count == 1 ? "single" : "aggregate",
                Results = results
            };
        }

        private Task<RawSyncResult> ExecuteByConfig(
            SyncRepositoryConfig config,
            DateTimeOffset? lastSync,
            Dictionary<string, string> param)
        {
            return config.SourceType switch
            {
                SyncSourceType.Local =>
                    ExecuteLocal(config, lastSync, param),

                SyncSourceType.Remote =>
                    _executionService.ExecuteRemoteAsync(
                        config.Endpoint,
                        lastSync,
                        param,
                        config.SourceName
                    ),

                _ => Task.FromResult(new RawSyncResult
                {
                    Ok = false,
                    ErrorCode = "INVALID_SOURCE_TYPE",
                    ErrorMessage = "Unknown source type",
                    Retryable = false,
                    Source = "Repository"
                })
            };
        }

        private Task<RawSyncResult> ExecuteLocal(
            SyncRepositoryConfig config,
            DateTimeOffset? lastSync,
            Dictionary<string, string> param)
        {
            return (Task<RawSyncResult>)typeof(ISyncExecutionService)
                .GetMethod(nameof(ISyncExecutionService.ExecuteLocalAsync))
                .MakeGenericMethod(config.EntityType)
                .Invoke(_executionService, new object[]
                {
                null,
                config.StoredProcedure,
                lastSync,
                param,
                config.SourceName
                });
        }
    }

}
