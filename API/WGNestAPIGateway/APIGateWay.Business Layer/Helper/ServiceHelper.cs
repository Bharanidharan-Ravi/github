using APIGateWay.BusinessLayer.Helper;
using APIGateWay.DomainLayer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper
{
    // 🔥 1. MUST BE 'public static' for extension methods to work!
    public static class ServiceHelper
    {
        public static async Task<T> FetchRichDataAsync<T>(
            this ISyncExecutionService syncExecutionService, // 🔥 'this' makes it an extension method
            string configKey,
            Dictionary<string, string> syncParams,
            Func<T, bool> matchPredicate,
            T fallbackData,
            DateTimeOffset? lastSync = null) where T : class
        {
            try
            {
                if (!SyncRepositoryConfigStore.Configs.TryGetValue(configKey, out var cfg))
                {
                    Console.WriteLine($"[RichDataRefetch] ConfigKey '{configKey}' not found.");
                    return fallbackData;
                }

                var syncResponse = await syncExecutionService.ExecuteLocalAsync<T>(
                    databaseName: "",
                    storedProcedure: cfg.StoredProcedure,
                    lastSync: lastSync,
                    parameters: syncParams,
                    source: "RichDataRefetchExtension"
                );

                if (syncResponse.Ok && syncResponse.Data != null)
                {
                    var items = syncResponse.Data as IEnumerable<T>;

                    if (items == null && syncResponse.Data is JsonElement jsonElement)
                    {
                        items = JsonSerializer.Deserialize<List<T>>(
                            jsonElement.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }

                    var richData = items?.FirstOrDefault(matchPredicate);
                    if (richData != null)
                    {
                        return richData;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RichDataRefetch] Failed to fetch rich data for {configKey}: {ex.Message}");
            }

            return fallbackData;
        }
    }
    
}
