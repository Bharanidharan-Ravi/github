using System;
using APIGateWay.ModalLayer.nugetmodal;


namespace APIGateWay.DomainLayer.Interface
{
    public interface ISyncExecutionService
    {
        Task<RawSyncResult> ExecuteRemoteAsync(
            string endpoint,
            DateTimeOffset? lastSync,
            Dictionary<string, string> parameters,
            string source
        );

        Task<RawSyncResult> ExecuteLocalAsync<T>(
            string databaseName,
            string storedProcedure,
            DateTimeOffset? lastSync,
            Dictionary<string, string> parameters,
            string source,
            bool processAttachments = false,
            string? attachmentPropertyName = null
        )
        where T : class;
    }
}
