using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.ModalLayer.nugetmodal
{
    public class DynamicSyncRequest
    {
        /// <summary>
        /// Keys that identify which modules to sync
        /// Example: ["ProjectList", "RepoList"]
        /// </summary>
        public List<string> ConfigKeys { get; set; } = new();

        /// <summary>
        /// Last successful sync timestamp per module
        /// Example:
        /// {
        ///   "ProjectList": "2026-01-01T10:00:00Z"
        /// }
        /// </summary>
        public Dictionary<string, DateTimeOffset?> Timestamps { get; set; } = new();

        /// <summary>
        /// Additional parameters per module
        /// Example:
        /// {
        ///   "ProjectList": { "RepoId": "101" },
        ///   "RepoList": { "Region": "IN" }
        /// }
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Params { get; set; } = new();
    }

}
