using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly string[] _skipPaths =
        {
            "/health",
            "/swagger",
            "/favicon.ico"
        };

        public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

        // ─────────────────────────────────────────────────────────────────────
        public async Task InvokeAsync(
            HttpContext context,
            IApiLoggerService logger,
            IRequestStepContext stepContext)
        {
            var path = context.Request.Path.Value ?? "";

            if (_skipPaths.Any(s => path.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            context.Request.EnableBuffering();

            var sw = Stopwatch.StartNew();
            string? body = await ReadRequestBodyAsync(context.Request);
            string? errorMsg = null;
            string? innerEx = null;
            string? stackTrace = null;

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                innerEx = ex.InnerException?.Message;
                stackTrace = ex.StackTrace;
                throw;
            }
            finally
            {
                sw.Stop();
                // 1. Check if an error was stashed in the Items dictionary
                if (context.Items.TryGetValue("CapturedError", out var stashedError))
                {
                    errorMsg ??= stashedError?.ToString();
                }

                if (context.Items.TryGetValue("CapturedStackTrace", out var stashedTrace))
                {
                    stackTrace ??= stashedTrace?.ToString();
                }

                if (context.Items.TryGetValue("CapturedInnerException", out var capturedInner))
                {
                    innerEx = capturedInner?.ToString();
                }

                string? userId = null;
                string? userName = null;
                try
                {
                    var loginCtx = context.RequestServices.GetService<ILoginContextService>();
                    userId = loginCtx?.userId.ToString();
                    userName = loginCtx?.userName;
                }
                catch { }

                var statusCode = context.Response.StatusCode;
                var logLevel = errorMsg != null ? "Error"
                               : statusCode >= 500 ? "Error"
                               : statusCode >= 400 ? "Warning"
                               : "Info";

                var indiaTimeZone =
               TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

                var indiaTime =
                    TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.UtcNow,
                        indiaTimeZone
                    );

                var log = new ApiLog
                {
                    Source = "HTTP",
                    Method = context.Request.Method,
                    Path = path,
                    QueryString = context.Request.QueryString.HasValue
                                         ? context.Request.QueryString.Value : null,
                    RequestBody = body,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserId = userId,
                    UserName = userName,
                    StatusCode = statusCode,
                    DurationMs = sw.ElapsedMilliseconds,
                    ErrorMessage = errorMsg,
                    InnerException = innerEx,
                    StackTrace = logLevel == "Error" ? stackTrace : null,
                    LogLevel = logLevel,
                    CreatedAt = indiaTime
                };

                // Hand the main log + all collected steps to the logger service
                await logger.WriteAsync(log, stepContext.GetSteps());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
        {
            if (request.ContentLength == null || request.ContentLength == 0) return null;

            if (!new[] { "POST", "PUT", "PATCH" }.Contains(
                    request.Method, StringComparer.OrdinalIgnoreCase)) return null;

            try
            {
                request.Body.Position = 0;
                using var reader = new StreamReader(request.Body, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
                return body;
            }
            catch { return null; }
        }
    }
}
