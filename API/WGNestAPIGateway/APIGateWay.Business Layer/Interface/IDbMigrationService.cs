using APIGateWay.ModalLayer.DTOs;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IDbMigrationService
    {
        /// <summary>Snapshot of the logged-in user's DB: structure .json + full .sql backup, recorded in SYS_DBSNAPSHOT.</summary>
        Task<DbSnapshotRecordDto> TakeSnapshotAsync(CreateDbSnapshotRequest request);

        Task<IEnumerable<DbSnapshotRecordDto>> ListAsync();

        Task<DbSnapshotRecordDto> GetAsync(int snapshotId);

        /// <summary>
        /// Compares an uploaded structure snapshot with the live DB and applies only new tables,
        /// new columns, column type changes and new/changed procedures — never data, never drops. Then snapshots.
        /// </summary>
        Task<DbMigrationReportDto> MigrateAsync(DbMigrateRequest request);

        /// <summary>Undoes the latest applied migration (tables/columns only dropped while empty).</summary>
        Task<DbRollbackReportDto> RollbackAsync(int migrationId);

        /// <summary>
        /// Full copy (structure + data) of one database into another on the same server, e.g. WG_APP -> WG_APP_TEST,
        /// via a COPY_ONLY backup and restore. Only TestConnection's database or DbMigration:CloneTargets may be the target.
        /// </summary>
        Task<DbCloneReportDto> CloneDatabaseAsync(DbCloneRequest request);
    }
}
