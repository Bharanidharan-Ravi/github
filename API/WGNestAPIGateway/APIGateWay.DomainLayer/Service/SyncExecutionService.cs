using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.nugetmodal;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace APIGateWay.DomainLayer.Service
{
    public class SyncExecutionService : ISyncExecutionService

    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _httpClient;
        private readonly APIGateWayCommonService _Service;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoginContextService _loginContext;

        public SyncExecutionService(
            HttpClient httpClient,
            APIGateWayCommonService commonService,
            IHttpContextAccessor httpContextAccessor,
            ILoginContextService loginContext,
            IServiceScopeFactory serviceScopeFactory
            )
        {
            _httpClient = httpClient;
            _Service = commonService;
            _httpContextAccessor = httpContextAccessor;
            _loginContext = loginContext;
            _scopeFactory = serviceScopeFactory;
        }
        public async Task<RawSyncResult> ExecuteRemoteAsync(
            string endpoint, DateTimeOffset? lastSync, Dictionary<string, string> parameters, string source)
        {
            try
            {
                var query = BuildQuery(lastSync, parameters);
                var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}{query}");

                var token = _httpContextAccessor.HttpContext?
                    .Request.Headers["WG_token"].FirstOrDefault();

                if (!string.IsNullOrEmpty(token))
                    request.Headers.Add("WG_token", token);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return new RawSyncResult
                    {
                        Ok = false,
                        ErrorCode = $"HTTP_{(int)response.StatusCode}",
                        ErrorMessage = response.ReasonPhrase,
                        Retryable = (int)response.StatusCode >= 500,
                        Source = source
                    };
                }

                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                object data;

                // ✅ CASE 1: Wrapped response { data: ... }
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("data", out var dataProp))
                {
                    data = dataProp.Clone();
                }
                // ✅ CASE 2: Raw array or object
                else if (root.ValueKind == JsonValueKind.Array ||
                         root.ValueKind == JsonValueKind.Object)
                {
                    data = root.Clone();
                }
                else
                {
                    return new RawSyncResult
                    {
                        Ok = false,
                        ErrorCode = "INVALID_REMOTE_RESPONSE",
                        ErrorMessage = "Unexpected response format",
                        Retryable = false,
                        Source = source
                    };
                }

                return new RawSyncResult
                {
                    Ok = true,
                    Data = data
                };
            }
            catch (JsonException ex)
            {
                return new RawSyncResult
                {
                    Ok = false,
                    ErrorCode = "REMOTE_JSON_ERROR",
                    ErrorMessage = ex.Message,
                    Retryable = false,
                    Source = source
                };
            }
            catch (Exception ex)
            {
                return new RawSyncResult
                {
                    Ok = false,
                    ErrorCode = "REMOTE_EXECUTION_ERROR",
                    ErrorMessage = ex.Message,
                    Retryable = true,
                    Source = source
                };
            }
        }


        public async Task<RawSyncResult> ExecuteLocalAsync<T>(
     string databaseName,
     string storedProcedure,
     DateTimeOffset? lastSync,
     Dictionary<string, string> parameters,
     string source)
     where T : class
        {
            try
            {
                var dbName = _loginContext.databaseName;

                var sqlParams = new List<SqlParameter>
{
    new SqlParameter("@DbName", dbName ?? (object)DBNull.Value)
};

                if (parameters != null)
                {
                    foreach (var kv in parameters)
                    {
                        if (!sqlParams.Any(p => p.ParameterName == $"@{kv.Key}"))
                        {
                            sqlParams.Add(
                                new SqlParameter($"@{kv.Key}", kv.Value ?? (object)DBNull.Value)
                            );
                        }
                    }
                }

                var data = await _Service.ExecuteGetItemAsyc<T>(
                    storedProcedure,
                    sqlParams.ToArray()
                );

                return new RawSyncResult
                {
                    Ok = true,
                    Data = data
                };
            }
            catch (SqlException ex)
            {
                return new RawSyncResult
                {
                    Ok = false,
                    ErrorCode = "DB_FAILURE",
                    ErrorMessage = ex.Message,
                    Retryable = true,
                    Source = source
                };
            }
            catch (Exception ex)
            {
                return new RawSyncResult
                {
                    Ok = false,
                    ErrorCode = "LOCAL_EXECUTION_ERROR",
                    ErrorMessage = ex.Message,
                    Retryable = false,
                    Source = source
                };
            }
        }


        private static string BuildQuery(
    DateTimeOffset? lastSync,
    Dictionary<string, string> parameters)
        {
            var query = new List<string>();

            if (lastSync.HasValue)
                query.Add($"since={Uri.EscapeDataString(lastSync.Value.ToString("o"))}");

            if (parameters != null)
            {
                foreach (var kv in parameters)
                    query.Add($"{kv.Key}={Uri.EscapeDataString(kv.Value)}");
            }

            return query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        }
    }

}

