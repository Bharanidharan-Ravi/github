using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class ApiLog
    {
        [Key]
        public long LogId { get; set; }      // PK — BIGINT IDENTITY

        // ── Source ────────────────────────────────────────────────────────────
        public string Source { get; set; } = "HTTP";  // HTTP | SignalR

        // ── Request info ──────────────────────────────────────────────────────
        public string Method { get; set; }      // GET, POST, PUT, PATCH, DELETE  |  HUB (SignalR)
        public string Path { get; set; }      // /api/ticket/CreateTicket        |  HubMethod name
        public string? QueryString { get; set; }
        public string? RequestBody { get; set; }      // Full JSON body
        public string? IpAddress { get; set; }

        // ── User context ──────────────────────────────────────────────────────
        public string? UserId { get; set; }
        public string? UserName { get; set; }

        // ── Response ──────────────────────────────────────────────────────────
        public int StatusCode { get; set; }      // 200, 400, 500 … | 0 for SignalR
        public long DurationMs { get; set; }

        // ── Error details (null on success) ───────────────────────────────────
        public string? ErrorMessage { get; set; }
        public string? InnerException { get; set; }      // ex.InnerException?.Message
        public string? StackTrace { get; set; }      // stored only on 500 / Error level

        // ── Audit ─────────────────────────────────────────────────────────────
        public string LogLevel { get; set; } = "Info";   // Info | Warning | Error
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation (EF) ───────────────────────────────────────────────────
        public ICollection<ApiLogStep> Steps { get; set; } = new List<ApiLogStep>();
    }
}
