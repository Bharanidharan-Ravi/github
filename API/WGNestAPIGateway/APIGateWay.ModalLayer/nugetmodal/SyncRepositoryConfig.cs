using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Configuration
{
    public enum SyncSourceType
    {
        Local,
        Remote
    }

    // Fields SyncRepositoryV2 can pull from ILoginContextService (the decoded JWT)
    // to inject into a Local SP call. Add a case here + one line in
    // SyncRepositoryV2.ResolveIdentityField to support a new field — never hardcode
    // a param name/value in the repository itself.
    public enum IdentityField
    {
        UserId,
        IsAdmin,
        Role,
        UserName
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
        public string NotificationTitle { get; set; }

        public string NotificationMessage { get; set; }

        public string SignalREntity { get; set; }

        public string SignalRAction { get; set; }

        // SP param name -> identity field to inject from ILoginContextService before
        // execution, e.g. { ["UserId"] = IdentityField.UserId, ["IsAdmin"] = IdentityField.IsAdmin }.
        // The frontend never sends these; SyncRepositoryV2 resolves and adds them.
        public Dictionary<string, IdentityField> IdentityParams { get; set; }
    }


}
