using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace APIGateWay.ModalLayer.DTOs
{
    /// <summary>Bound from appsettings "DbMigration".</summary>
    public class DbMigrationOptions
    {
        public const string SectionName = "DbMigration";

        /// <summary>Endpoints answer 403 unless this is true.</summary>
        public bool Enabled { get; set; }

        /// <summary>Folder for snapshot, uploaded and rollback files (created if missing).</summary>
        public string SnapshotPath { get; set; } = string.Empty;

        /// <summary>Default for a manual snapshot's full .sql backup including table data (INSERTs).</summary>
        public bool IncludeData { get; set; } = true;

        /// <summary>SQL Server schema that is snapshotted and migrated.</summary>
        public string Schema { get; set; } = "dbo";

        public int CommandTimeoutSeconds { get; set; } = 600;

        /// <summary>
        /// Databases /clone may overwrite, besides TestConnection's database (always allowed).
        /// DefaultConnection's (live) database is never a valid target, even if listed.
        /// </summary>
        public List<string> CloneTargets { get; set; } = new();

        /// <summary>Folder ON THE SQL SERVER for the clone's .bak file; empty = the instance's default backup folder.</summary>
        public string? CloneBackupPath { get; set; }

        /// <summary>Timeout of the BACKUP and RESTORE statements of /clone.</summary>
        public int CloneTimeoutSeconds { get; set; } = 3600;
    }

    public class DbCloneRequest
    {
        /// <summary>Database to copy (structure + data). Defaults to DefaultConnection's database (live).</summary>
        public string? SourceDatabase { get; set; }

        /// <summary>Database to create/replace. Defaults to TestConnection's database.</summary>
        public string? TargetDatabase { get; set; }

        /// <summary>Must be true when the target already exists: it is dropped and replaced by the copy.</summary>
        public bool Overwrite { get; set; }

        /// <summary>Point procedures/views/functions/triggers that name the source DB (WG_APP.dbo.X) at the target. Default true.</summary>
        public bool RewriteReferences { get; set; } = true;

        /// <summary>Only check and return the plan; nothing is changed. Defaults to true — send false to clone.</summary>
        public bool? DryRun { get; set; }
    }

    public class DbCloneReportDto
    {
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public string? Error { get; set; }
        public string Server { get; set; } = string.Empty;
        public string SourceDatabase { get; set; } = string.Empty;
        public string TargetDatabase { get; set; } = string.Empty;
        public bool TargetExisted { get; set; }
        public long? SourceSizeMb { get; set; }
        /// <summary>The COPY_ONLY backup, on the SQL Server machine. Kept so the clone can be restored again.</summary>
        public string? BackupFile { get; set; }
        /// <summary>Logical file -> new physical file of the target.</summary>
        public List<string> Files { get; set; } = new();
        /// <summary>Modules that name the source DB and were (or, on a dry run, would be) pointed at the target.</summary>
        public List<string> RewrittenModules { get; set; } = new();
        public List<DbMigrationNoteDto> RewriteFailures { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public int? CloneId { get; set; }
    }

    /// <summary>SYS_DBSNAPSHOT row (SP_SYS_DBSNAPSHOT_LIST).</summary>
    public class DbSnapshotRecordDto
    {
        public int SnapshotId { get; set; }
        public string SnapshotType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? SqlFilePath { get; set; }
        public string? RollbackFilePath { get; set; }
        public long? SizeBytes { get; set; }
        public string? Summary { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateDbSnapshotRequest
    {
        /// <summary>Include table data (INSERTs) in the .sql backup; defaults to DbMigration:IncludeData.</summary>
        public bool? IncludeData { get; set; }
    }

    public class DbMigrateRequest
    {
        /// <summary>Structure snapshot (.json) taken from the source DB, e.g. WG_APP_TEST.</summary>
        public IFormFile? File { get; set; }

        /// <summary>Only compare and return the plan; nothing is changed. Defaults to true — send false to apply.</summary>
        public bool? DryRun { get; set; }

        /// <summary>
        /// Other databases the procedures read (db.schema.object), renamed for the target server,
        /// e.g. "WG_APP_TEST=WG_APP" (several separated by ';'). The source database itself is
        /// always mapped to the target automatically.
        /// </summary>
        public string? DatabaseMap { get; set; }

        /// <summary>
        /// Apply only these, separated by ',' or new lines: a table or procedure name (all its steps)
        /// or Table.Column (that column only), e.g. "HolidayMaster, GetHolidays, MeetingMaster.times_per_day".
        /// Empty = every planned step.
        /// </summary>
        public string? Objects { get; set; }

        /// <summary>Leave these out (same format as Objects), applied after Objects.</summary>
        public string? Exclude { get; set; }
    }

    public class DbMigrationStepDto
    {
        public string Kind { get; set; } = string.Empty;   // CREATE_TABLE | ADD_COLUMN | ALTER_COLUMN | CREATE_PROCEDURE | UPDATE_PROCEDURE
        public string Object { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // PLANNED | APPLIED | FAILED | NOT_RUN | ROLLED_BACK | KEPT
        public string? Message { get; set; }
    }

    public class DbMigrationNoteDto
    {
        public string Object { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class DbMigrationReportDto
    {
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public string? Error { get; set; }
        public string SourceDatabase { get; set; } = string.Empty;
        public string TargetDatabase { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int? MigrationId { get; set; }
        public DbSnapshotRecordDto? PostSnapshot { get; set; }
        public List<DbMigrationStepDto> Steps { get; set; } = new();
        /// <summary>Differences that are NOT applied automatically (type changes, views, ...).</summary>
        public List<DbMigrationNoteDto> Manual { get; set; } = new();
        /// <summary>Planned steps left out by Objects / Exclude.</summary>
        public List<string> NotSelected { get; set; } = new();
        /// <summary>Objects/columns that exist only in the target — reported, never dropped.</summary>
        public List<string> OnlyInTarget { get; set; } = new();
    }

    public class DbRollbackReportDto
    {
        public int MigrationId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<DbMigrationStepDto> Steps { get; set; } = new();
    }
}
