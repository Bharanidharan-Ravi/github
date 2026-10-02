namespace APIGateWay.Business_Layer.DbMigration
{
    /// <summary>
    /// Constants for the DB snapshot / schema-migration APIs (DbMigrationService). Tracking table,
    /// its SPs and the embedded bootstrap script come from DbMigration/DbSnapshot.sql.
    /// </summary>
    public static class DbMigrationConstants
    {
        public const string TRACKING_TABLE = "SYS_DBSNAPSHOT";
        public const string SP_SNAPSHOT_SAVE = "dbo.SP_SYS_DBSNAPSHOT_SAVE";
        public const string SP_SNAPSHOT_LIST = "dbo.SP_SYS_DBSNAPSHOT_LIST";
        public const string SP_SNAPSHOT_SET_STATUS = "dbo.SP_SYS_DBSNAPSHOT_SET_STATUS";

        // Embedded copy of DbSnapshot.sql (LogicalName in APIGateWay.BusinessLayer.csproj).
        public const string BOOTSTRAP_RESOURCE = "APIGateWay.Business_Layer.DbSnapshotBootstrap.sql";

        // SNAPSHOTTYPE
        public const string TYPE_MANUAL = "MANUAL";
        public const string TYPE_POST_MIGRATE = "POST_MIGRATE";
        public const string TYPE_MIGRATION = "MIGRATION";

        // STATUS
        public const string STATUS_DONE = "DONE";
        public const string STATUS_APPLIED = "APPLIED";
        public const string STATUS_ROLLED_BACK = "ROLLED_BACK";
        public const string STATUS_FAILED = "FAILED";

        // Structure snapshot file format (bumped if SchemaSnapshot changes shape).
        public const int SNAPSHOT_FORMAT_VERSION = 1;
    }
}
