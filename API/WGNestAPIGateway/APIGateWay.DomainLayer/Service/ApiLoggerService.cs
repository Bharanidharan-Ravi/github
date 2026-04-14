using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IoFile = System.IO.File;

namespace APIGateWay.DomainLayer.Service
{
    public class ApiLoggerService : IApiLoggerService
    {
        private readonly APIGatewayDBContext _db;
        private readonly string _fallbackFolder;

        public ApiLoggerService(APIGatewayDBContext db, IConfiguration configuration)
        {
            _db = db;
            _fallbackFolder = configuration["LogSettings:FallbackLogFolder"]
                              ?? Path.Combine(AppContext.BaseDirectory, "Logs");
        }

        // ── HTTP request log (called by middleware) ───────────────────────────
        public async Task WriteAsync(ApiLog log, List<ApiLogStep> steps)
        {
            try
            {
                _db.ApiLogs.Add(log);
                await _db.SaveChangesAsync();   // log is now saved — LogId is populated

                // Link every step to this log and save them
                if (steps != null && steps.Any())
                {
                    foreach (var step in steps)
                        step.LogId = log.LogId;

                    _db.ApiLogSteps.AddRange(steps);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception sqlEx)
            {
                try { WriteFallbackLog(log, steps, sqlEx); }
                catch { /* swallow — logging must never crash the API */ }
            }
        }

        // ── SignalR hub log ───────────────────────────────────────────────────
        public async Task WriteSignalRAsync(
            string hubMethod,
            string? payload,
            string? userId,
            string? userName,
            string? errorMessage = null,
            string? innerException = null)
        {
            var log = new ApiLog
            {
                Source = "SignalR",
                Method = "HUB",
                Path = hubMethod,         // e.g. "SendTicketUpdate"
                RequestBody = payload,
                UserId = userId,
                UserName = userName,
                StatusCode = 0,                 // N/A for SignalR
                DurationMs = 0,
                ErrorMessage = errorMessage,
                InnerException = innerException,
                LogLevel = errorMessage != null ? "Error" : "Info",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _db.ApiLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception sqlEx)
            {
                try { WriteFallbackLog(log, null, sqlEx); }
                catch { }
            }
        }

        // ── File fallback ─────────────────────────────────────────────────────
        private void WriteFallbackLog(ApiLog log, List<ApiLogStep>? steps, Exception sqlEx)
        {
            System.IO.Directory.CreateDirectory(_fallbackFolder);

            var fileName = $"APIGateway_{DateTime.UtcNow:yyyy-MM-dd}.log";
            var filePath = System.IO.Path.Combine(_fallbackFolder, fileName);
            var sep = new string('-', 80);

            var lines = new List<string>
            {
                sep,
                $"[{log.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC]  Level: {log.LogLevel}  Source: {log.Source}",
                $"  {log.Method,-7} {log.Path}{log.QueryString}",
                $"  Status : {log.StatusCode}   Duration: {log.DurationMs} ms",
                $"  User   : {log.UserId} ({log.UserName})   IP: {log.IpAddress}"
            };

            if (!string.IsNullOrWhiteSpace(log.RequestBody))
                lines.Add($"  Body   : {log.RequestBody}");

            if (!string.IsNullOrWhiteSpace(log.ErrorMessage))
                lines.Add($"  ERROR  : {log.ErrorMessage}");

            if (!string.IsNullOrWhiteSpace(log.InnerException))
                lines.Add($"  INNER  : {log.InnerException}");

            if (!string.IsNullOrWhiteSpace(log.StackTrace))
                lines.Add($"  TRACE  : {log.StackTrace}");

            // Write step breakdown if available
            if (steps != null && steps.Any())
            {
                lines.Add("  STEPS:");
                foreach (var s in steps.OrderBy(x => x.StepOrder))
                {
                    var stepLine = $"    [{s.StepOrder}] {s.TableName,-25} {s.Operation,-8} {s.Status,-8}  {s.DurationMs}ms";
                    if (s.InsertedId != null) stepLine += $"  Id={s.InsertedId}";
                    if (s.ErrorMessage != null) stepLine += $"  ERR={s.ErrorMessage}";
                    lines.Add(stepLine);
                }
            }

            lines.Add($"  [SQL-FALLBACK] Could not write to DB: {sqlEx.Message}");
            lines.Add(sep);
            lines.Add(string.Empty);

            IoFile.AppendAllLinesAsync(filePath, lines);
        }
    }
}
