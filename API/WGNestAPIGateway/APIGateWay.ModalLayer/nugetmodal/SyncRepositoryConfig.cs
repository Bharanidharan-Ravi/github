using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Configuration
{
    public enum SyncSourceType
    {
        Local,
        Remote
    }

    public class SyncRepositoryConfig
    {
        // -------- Execution --------
        public SyncSourceType SourceType { get; set; }

        // Local
        public string StoredProcedure { get; set; }
        public Type EntityType { get; set; }

        // Remote
        public string Endpoint { get; set; }

        public string SourceName { get; set; }

        // -------- Aggregation / UI hints --------
        public string Type { get; set; }        // array | object
        public string Strategy { get; set; }    // merge | replace
        public string IdKey { get; set; }        // projectId, repoId
        public bool DeltaEnabled { get; set; }  // incremental sync
    }


}
