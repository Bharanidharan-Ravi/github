using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.MasterData
{
    public class ApiLogStep
    {
        [Key]
        public long StepId { get; set; }       // PK — BIGINT IDENTITY
        public long LogId { get; set; }       // FK → ApiLogs.LogId

        // ── What happened ─────────────────────────────────────────────────────
        public int StepOrder { get; set; }       // 1, 2, 3 … execution sequence
        public string TableName { get; set; }       // "IssueMasters", "AttachmentMaster" …
        public string Operation { get; set; }       // INSERT | UPDATE | DELETE

        // ── Outcome ───────────────────────────────────────────────────────────
        public string Status { get; set; }       // Success | Failed
        public string? InsertedId { get; set; }       // PK of inserted/updated row (any type → string)
        public long DurationMs { get; set; }       // How long this single table op took

        // ── Error (null on success) ───────────────────────────────────────────
        public string? ErrorMessage { get; set; }
        public string? InnerException { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation (EF) ───────────────────────────────────────────────────
        public ApiLog? Log { get; set; }
    }
}
