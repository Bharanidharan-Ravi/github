using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.SignalRHub.Middleware
{
    public class RealtimeBroadcastMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RealtimeBroadcastPipeline _pipeline;
        private readonly ILogger<RealtimeBroadcastMiddleware> _logger;

        // Pre-compiled regex cache keyed by route pattern for performance
        private readonly Dictionary<string, Regex> _regexCache = new();

        public RealtimeBroadcastMiddleware(
            RequestDelegate next,
            RealtimeBroadcastPipeline pipeline,
            ILogger<RealtimeBroadcastMiddleware> logger)
        {
            _next = next;
            _pipeline = pipeline;
            _logger = logger;

            // Pre-compile all route regexes at startup (not per-request)
            foreach (var entry in pipeline.Entries)
            {
                _regexCache[entry.RoutePattern] = BuildRouteRegex(entry.RoutePattern);
            }
        }

        public async Task InvokeAsync(HttpContext context, IRealtimeBroadcaster broadcaster)
        {
            // ── 1. Fast path: skip if no route matches ────────────────────────
            var entry = MatchEntry(context);

            if (entry is null)
            {
                await _next(context);
                return;
            }

            // ── 2. Buffer the response body ───────────────────────────────────
            var originalBody = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);
            }
            finally
            {
                // Always restore — even if the action threw
                context.Response.Body = originalBody;
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody);
            }

            // ── 3. Only broadcast on success (2xx) ────────────────────────────
            if (!IsSuccess(context.Response.StatusCode))
            {
                _logger.LogDebug(
                    "[RealtimeMiddleware] Skipped — status {Code} for {Method} {Path}",
                    context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Path);
                return;
            }

            // ── 4. Read the captured response JSON ────────────────────────────
            buffer.Position = 0;
            var responseJson = await new StreamReader(buffer, Encoding.UTF8)
                .ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(responseJson)) return;

            // ── 5. Fire broadcast — completely decoupled from the response ────
            // Task.Run so the client receives their response immediately.
            // The broadcaster internally swallows exceptions and logs them.
            _ = Task.Run(async () =>
            {
                _logger.LogDebug(
                    "[RealtimeMiddleware] Broadcasting {Entity} {Action}",
                    entry.Action, entry.HttpMethod);

                await entry.BroadcastAsync(responseJson, broadcaster);
            });
        }

        // ── Route matching ─────────────────────────────────────────────────────
        private IBroadcastRouteEntry? MatchEntry(HttpContext context)
        {
            var method = context.Request.Method.ToUpperInvariant();
            var path = context.Request.Path.Value ?? string.Empty;

            foreach (var entry in _pipeline.Entries)
            {
                if (!string.Equals(entry.HttpMethod, method, StringComparison.Ordinal))
                    continue;

                if (_regexCache.TryGetValue(entry.RoutePattern, out var regex)
                    && regex.IsMatch(path))
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Converts "/api/Ticket/Update/{id}" to a compiled Regex
        /// that matches any value in the {param} segments.
        /// </summary>
        private static Regex BuildRouteRegex(string pattern)
        {
            var escaped = Regex.Escape(pattern);

            // Replace escaped {param} placeholders: \{[^\}]+\} → [^/]+
            var regexStr = Regex.Replace(escaped, @"\\\{[^}]+\}", "[^/]+");

            return new Regex(
                $"^{regexStr}/?$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        private static bool IsSuccess(int statusCode) =>
            statusCode is >= 200 and < 300;
    }
}
