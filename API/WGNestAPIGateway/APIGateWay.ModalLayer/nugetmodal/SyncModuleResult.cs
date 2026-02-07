using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.nugetmodal
{
    public class SyncResponse
    {
        /// <summary>
        /// Overall request execution status (request-level, not per-module)
        /// </summary>
        public bool Ok { get; set; }

        /// <summary>
        /// Unique id for tracing/debugging
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Server time when response was generated
        /// </summary>
        public DateTimeOffset ServerTime { get; set; }

        /// <summary>
        /// single | aggregate
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Per-module results keyed by ConfigKey
        /// </summary>
        public Dictionary<string, SyncModuleResult> Results { get; set; } = new();

        /// <summary>
        /// Root-level error (used only if whole request fails)
        /// </summary>
        public SyncError Errors { get; set; }
    }

    public class SyncModuleResult
    {
        /// <summary>
        /// Module execution status
        /// </summary>
        public bool Ok { get; set; }

        /// <summary>
        /// array | object | aggregate
        /// Helps UI engine understand data shape
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// merge | replace
        /// Tells UI how to update state
        /// </summary>
        public string Strategy { get; set; }

        /// <summary>
        /// Primary key for merge strategy (e.g., repoId, projectId)
        /// </summary>
        public string IdKey { get; set; }

        /// <summary>
        /// Actual data payload
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Sync metadata (timestamps, delta info)
        /// </summary>
        public SyncMeta Meta { get; set; }

        /// <summary>
        /// Error details if Ok = false
        /// </summary>
        public SyncError Error { get; set; }
    }

    public class SyncMeta
    {
        public int Count { get; set; }
        public bool Delta { get; set; }
        public DateTimeOffset LastSync { get; set; }
    }

    public class SyncError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; }  // error | warning | critical
        public bool Retryable { get; set; }
        public string Source { get; set; }
    }

}
