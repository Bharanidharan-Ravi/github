using APIGateWay.BusinessLayer.Auth;
using APIGateWay.BusinessLayer.Configuration;
using APIGateWay.BusinessLayer.Helper;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.nugerModalV2;
using APIGateWay.ModalLayer.nugetmodal;
using System;
using System.Text.Json;

namespace APIGateWay.BusinessLayer.Repository
{
    public class SyncRepositoryV2 : ISyncRepositoryV2
    {
        private readonly ISyncExecutionService _exec;

        public SyncRepositoryV2(ISyncExecutionService exec) => _exec = exec;

        // ─────────────────────────────────────────────────────────────────────
        // Primary path — called by SyncV2Controller with enriched units
        // ─────────────────────────────────────────────────────────────────────
        public async Task<SyncResponseV2> ExecuteUnitsAsync(List<SyncExecutionUnit> units)
        {
            // Group by ResultKey — multiple units per key for multi-repo fans
            var taskGroups = new Dictionary<string, List<Task<RawSyncResult>>>(StringComparer.Ordinal);

            foreach (var group in units.GroupBy(u => u.ResultKey))
            {
                taskGroups[group.Key] = new List<Task<RawSyncResult>>();

                foreach (var unit in group)
                {
                    if (!SyncRepositoryConfigStore.Configs.TryGetValue(unit.ConfigKey, out var cfg))
                    {
                        taskGroups[group.Key].Add(Task.FromResult(new RawSyncResult
                        {
                            Ok = false,
                            ErrorCode = "INVALID_CONFIG_KEY",
                            ErrorMessage = "Invalid setup. Contact admin.",
                            Retryable = false,
                            Source = "Repository"
                        }));
                        continue;
                    }

                    taskGroups[group.Key].Add(cfg.SourceType switch
                    {
                        SyncSourceType.Local =>
                            ExecuteLocal(cfg, unit.LastSync, unit.Params),
                        SyncSourceType.Remote =>
                            _exec.ExecuteRemoteAsync(cfg.Endpoint, unit.LastSync, unit.Params, cfg.SourceName),
                        _ => Task.FromResult(new RawSyncResult
                        {
                            Ok = false,
                            ErrorCode = "INVALID_SOURCE_TYPE",
                            ErrorMessage = "Config error. Contact admin.",
                            Retryable = false,
                            Source = "Repository"
                        })
                    });
                }
            }

            // Run every task in parallel — across all keys and all repos
            await Task.WhenAll(taskGroups.Values.SelectMany(t => t));

            var response = NewResponse();

            foreach (var (resultKey, tasks) in taskGroups)
            {
                var results = tasks.Select(t => t.Result).ToList();

                // All failed → return first error
                if (results.All(r => !r.Ok))
                {
                    var first = results.First();
                    response.Res[resultKey] = new SyncResultV2
                    {
                        Ok = false,
                        Err = new SyncErrorV2 { C = first.ErrorCode, M = first.ErrorMessage, R = first.Retryable }
                    };
                    continue;
                }

                // Merge successful rows from all repos into one flat array
                var mergedRows = new List<JsonElement>();
                foreach (var r in results.Where(r => r.Ok && r.Data != null))
                {
                    // Data from local SP is List<T> serialized as array
                    // Data from remote is already a cloned JsonElement
                    var element = r.Data is JsonElement je
                        ? je
                        : JsonSerializer.SerializeToElement(r.Data);

                    if (element.ValueKind == JsonValueKind.Array)
                        foreach (var row in element.EnumerateArray())
                            mergedRows.Add(row);
                }

                response.Res[resultKey] = new SyncResultV2
                {
                    Ok = true,
                    Data = mergedRows.Count > 0
                        ? JsonSerializer.SerializeToElement(mergedRows)
                        : JsonSerializer.SerializeToElement(Array.Empty<object>())
                };
            }

            return response;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Legacy path — kept for backwards compatibility
        // ─────────────────────────────────────────────────────────────────────
        public async Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request)
        {
            var tasks = new Dictionary<string, Task<RawSyncResult>>();

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
                    SyncSourceType.Local => ExecuteLocal(cfg, lastSync, param),
                    SyncSourceType.Remote => _exec.ExecuteRemoteAsync(cfg.Endpoint, lastSync, param, cfg.SourceName),
                    _ => Task.FromResult(new RawSyncResult
                    {
                        Ok = false,
                        ErrorCode = "INVALID_SOURCE_TYPE",
                        ErrorMessage = "Config error.",
                        Retryable = false,
                        Source = "Repository"
                    })
                };
            }

            await Task.WhenAll(tasks.Values);
            var response = NewResponse();

            foreach (var kv in tasks)
            {
                var raw = kv.Value.Result;
                response.Res[kv.Key] = raw.Ok
                    ? new SyncResultV2 { Ok = true, Data = raw.Data }
                    : new SyncResultV2 { Ok = false, Err = new SyncErrorV2 { C = raw.ErrorCode, M = raw.ErrorMessage, R = raw.Retryable } };
            }

            return response;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reflection-based generic local execution (matches existing pattern)
        // ─────────────────────────────────────────────────────────────────────
        private Task<RawSyncResult> ExecuteLocal(
            SyncRepositoryConfig cfg,
            DateTimeOffset? lastSync,
            Dictionary<string, string>? param) =>
            (Task<RawSyncResult>)typeof(ISyncExecutionService)
                .GetMethod(nameof(ISyncExecutionService.ExecuteLocalAsync))!
                .MakeGenericMethod(cfg.EntityType)
                .Invoke(_exec, new object?[]
                {
                    null,                  // databaseName (resolved internally)
                    cfg.StoredProcedure,
                    lastSync,
                    param,
                    cfg.SourceName
                })!;

        private static SyncResponseV2 NewResponse() => new()
        {
            Rid = Guid.NewGuid().ToString(),
            St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

}


//public class SyncRepositoryV2 : ISyncRepositoryV2
//{
//    private readonly ISyncExecutionService _executionService;

//    public SyncRepositoryV2(ISyncExecutionService executionService)
//    {
//        _executionService = executionService;
//    }

//    //public async Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request)
//    //{
//    //    var tasks = new Dictionary<string, Task<RawSyncResult>>();

//    //    // -------- Build execution plan (config-driven) --------
//    //    foreach (var key in request.ConfigKeys)
//    //    {
//    //        if (!SyncRepositoryConfigStore.Configs.TryGetValue(key, out var cfg))
//    //        {
//    //            tasks[key] = Task.FromResult(new RawSyncResult
//    //            {
//    //                Ok = false,
//    //                ErrorCode = "INVALID_CONFIG_KEY",
//    //                ErrorMessage = "Invalid setup. Contact admin.",
//    //                Retryable = false,
//    //                Source = "Repository"
//    //            });
//    //            continue;
//    //        }

//    //        request.Timestamps.TryGetValue(key, out var lastSync);
//    //        request.Params.TryGetValue(key, out var param);

//    //        tasks[key] = cfg.SourceType switch
//    //        {
//    //            SyncSourceType.Local =>
//    //                ExecuteLocal(cfg, lastSync, param),

//    //            SyncSourceType.Remote =>
//    //                _executionService.ExecuteRemoteAsync(
//    //                    cfg.Endpoint,
//    //                    lastSync,
//    //                    param,
//    //                    cfg.SourceName
//    //                ),

//    //            _ => Task.FromResult(new RawSyncResult
//    //            {
//    //                Ok = false,
//    //                ErrorCode = "INVALID_SOURCE_TYPE",
//    //                ErrorMessage = "Config error. Contact admin.",
//    //                Retryable = false,
//    //                Source = "Repository"
//    //            })
//    //        };
//    //    }

//    //    // -------- Execute in parallel --------
//    //    await Task.WhenAll(tasks.Values);

//    //    // -------- Build v2 compact response --------
//    //    var response = new SyncResponseV2
//    //    {
//    //        Rid = Guid.NewGuid().ToString(),
//    //        St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
//    //    };

//    //    foreach (var kv in tasks)
//    //    {
//    //        var raw = kv.Value.Result;

//    //        if (raw.Ok)
//    //        {
//    //            response.Res[kv.Key] = new SyncResultV2
//    //            {
//    //                Ok = true,
//    //                Data = raw.Data
//    //            };
//    //        }
//    //        else
//    //        {
//    //            response.Res[kv.Key] = new SyncResultV2
//    //            {
//    //                Ok = false,
//    //                Err = new SyncErrorV2
//    //                {
//    //                    C = raw.ErrorCode,
//    //                    M = raw.ErrorMessage,
//    //                    R = raw.Retryable
//    //                }
//    //            };
//    //        }
//    //    }

//    //    return response;
//    //}
//    // ── Original method — unchanged ───────────────────────────────────────
//    public async Task<SyncResponseV2> ExecuteAsync(DynamicSyncRequest request)
//    {
//        var tasks = new Dictionary<string, Task<RawSyncResult>>();

//        foreach (var key in request.ConfigKeys)
//        {
//            if (!SyncRepositoryConfigStore.Configs.TryGetValue(key, out var cfg))
//            {
//                tasks[key] = Task.FromResult(new RawSyncResult
//                {
//                    Ok = false,
//                    ErrorCode = "INVALID_CONFIG_KEY",
//                    ErrorMessage = "Invalid setup. Contact admin.",
//                    Retryable = false,
//                    Source = "Repository"
//                });
//                continue;
//            }

//            request.Timestamps.TryGetValue(key, out var lastSync);
//            request.Params.TryGetValue(key, out var param);

//            tasks[key] = cfg.SourceType switch
//            {
//                SyncSourceType.Local => ExecuteLocal(cfg, lastSync, param),
//                SyncSourceType.Remote => _executionService.ExecuteRemoteAsync(cfg.Endpoint, lastSync, param, cfg.SourceName),
//                _ => Task.FromResult(new RawSyncResult { Ok = false, ErrorCode = "INVALID_SOURCE_TYPE", ErrorMessage = "Config error.", Retryable = false, Source = "Repository" })
//            };
//        }

//        await Task.WhenAll(tasks.Values);

//        var response = new SyncResponseV2
//        {
//            Rid = Guid.NewGuid().ToString(),
//            St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
//        };

//        foreach (var kv in tasks)
//        {
//            var raw = kv.Value.Result;
//            response.Res[kv.Key] = raw.Ok
//                ? new SyncResultV2 { Ok = true, Data = raw.Data }
//                : new SyncResultV2 { Ok = false, Err = new SyncErrorV2 { C = raw.ErrorCode, M = raw.ErrorMessage, R = raw.Retryable } };
//        }

//        return response;
//    }

//    // ── ADD THIS: executes enriched units, merges multi-repo results ──────
//    //
//    // For Role 3 with 2 repos:
//    //   units = [
//    //     { ConfigKey: "TicketsList", Params: { repoId: "guid-1" } },
//    //     { ConfigKey: "TicketsList", Params: { repoId: "guid-2" } },
//    //     { ConfigKey: "EmployeeList", Params: {} }
//    //   ]
//    //
//    // Both TicketsList units run in parallel.
//    // Results are merged: final TicketsList.Data = repo1 rows + repo2 rows.
//    public async Task<SyncResponseV2> ExecuteUnitsAsync(List<SyncExecutionUnit> units)
//    {
//        // Group by ResultKey — multiple units for same key get merged
//        var grouped = units.GroupBy(u => u.ResultKey);

//        // Task list per result key
//        var taskGroups = new Dictionary<string, List<Task<RawSyncResult>>>();

//        foreach (var group in grouped)
//        {
//            taskGroups[group.Key] = new List<Task<RawSyncResult>>();

//            foreach (var unit in group)
//            {
//                if (!SyncRepositoryConfigStore.Configs.TryGetValue(unit.ConfigKey, out var cfg))
//                {
//                    taskGroups[group.Key].Add(Task.FromResult(new RawSyncResult
//                    {
//                        Ok = false,
//                        ErrorCode = "INVALID_CONFIG_KEY",
//                        ErrorMessage = "Invalid setup. Contact admin.",
//                        Retryable = false,
//                        Source = "Repository"
//                    }));
//                    continue;
//                }

//                var task = cfg.SourceType switch
//                {
//                    SyncSourceType.Local => ExecuteLocal(cfg, unit.LastSync, unit.Params),
//                    SyncSourceType.Remote => _executionService.ExecuteRemoteAsync(cfg.Endpoint, unit.LastSync, unit.Params, cfg.SourceName),
//                    _ => Task.FromResult(new RawSyncResult { Ok = false, ErrorCode = "INVALID_SOURCE_TYPE", ErrorMessage = "Config error.", Retryable = false, Source = "Repository" })
//                };

//                taskGroups[group.Key].Add(task);
//            }
//        }

//        // Run all tasks in parallel (across all keys and all repos)
//        var allTasks = taskGroups.Values.SelectMany(t => t);
//        await Task.WhenAll(allTasks);

//        // Build response — merge results for same ResultKey
//        var response = new SyncResponseV2
//        {
//            Rid = Guid.NewGuid().ToString(),
//            St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
//        };

//        foreach (var (resultKey, tasks) in taskGroups)
//        {
//            var results = tasks.Select(t => t.Result).ToList();
//            var failed = results.FirstOrDefault(r => !r.Ok);

//            if (failed != null && results.All(r => !r.Ok))
//            {
//                // All executions failed — return first error
//                response.Res[resultKey] = new SyncResultV2
//                {
//                    Ok = false,
//                    Err = new SyncErrorV2 { C = failed.ErrorCode, M = failed.ErrorMessage, R = failed.Retryable }
//                };
//                continue;
//            }

//            // Merge all successful rows into one flat array
//            // Each result.Data is a JsonElement array (your existing pattern)
//            var mergedRows = new List<JsonElement>();

//            foreach (var result in results.Where(r => r.Ok && r.Data != null))
//            {
//                if (result.Data is JsonElement element && element.ValueKind == JsonValueKind.Array)
//                {
//                    foreach (var row in element.EnumerateArray())
//                        mergedRows.Add(row);
//                }
//            }

//            response.Res[resultKey] = new SyncResultV2
//            {
//                Ok = true,
//                Data = mergedRows.Count > 0
//                    ? JsonSerializer.SerializeToElement(mergedRows)
//                    : JsonSerializer.SerializeToElement(Array.Empty<object>())
//            };
//        }

//        return response;
//    }
//    // -------- Local execution (generic, config-driven) --------
//    private Task<RawSyncResult> ExecuteLocal(
//        SyncRepositoryConfig cfg,
//        DateTimeOffset? lastSync,
//        Dictionary<string, string> param)
//    {
//        return (Task<RawSyncResult>)typeof(ISyncExecutionService)
//            .GetMethod(nameof(ISyncExecutionService.ExecuteLocalAsync))
//            .MakeGenericMethod(cfg.EntityType)
//            .Invoke(_executionService, new object[]
//            {
//            null,                    // databaseName (handled internally)
//            cfg.StoredProcedure,
//            lastSync,
//            param,
//            cfg.SourceName
//            });
//    }
//}