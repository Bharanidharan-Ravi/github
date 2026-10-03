using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using APIGateWay.Business_Layer.DbMigration;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Utilities;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModelLayer.ErrorException;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static APIGateWay.Business_Layer.DbMigration.SqlSchemaReader;

namespace APIGateWay.Business_Layer.Repository
{
    /// <summary>
    /// Snapshots the SQL Server DB in the connection string (see OpenAsync) and applies
    /// schema-only migrations from an uploaded structure snapshot. Table data is never inserted, updated
    /// or deleted, and nothing is dropped — except by a rollback, which only drops a table/column it
    /// created while empty. A migration runs in one transaction, so a failure leaves the DB untouched.
    /// DDL and catalog reads cannot go through stored procedures, so this service runs SQL text;
    /// its own bookkeeping goes through the SP_SYS_DBSNAPSHOT_* SPs.
    /// </summary>
    public class DbMigrationService : IDbMigrationService
    {
        private const string STEP_PLANNED = "PLANNED";
        private const string STEP_APPLIED = "APPLIED";
        private const string STEP_FAILED = "FAILED";
        private const string STEP_NOT_RUN = "NOT_RUN";
        private const string STEP_ROLLED_BACK = "ROLLED_BACK";
        private const string STEP_KEPT = "KEPT";
        private const string DEFAULT_SETTINGS = "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;";

        // One snapshot/migration/rollback at a time per API process.
        private static readonly SemaphoreSlim Gate = new(1, 1);
        // Databases (server|db) whose SYS_DBSNAPSHOT + SPs are known to exist.
        private static readonly ConcurrentDictionary<string, bool> TrackingReady = new(StringComparer.OrdinalIgnoreCase);
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IEnvironmentRoutingService _environmentRouting;
        private readonly ILoginContextService _loginContext;
        private readonly IConfiguration _configuration;
        private readonly DbMigrationOptions _options;
        private readonly ILogger<DbMigrationService> _logger;

        public DbMigrationService(
            IEnvironmentRoutingService environmentRouting,
            ILoginContextService loginContext,
            IConfiguration configuration,
            IOptions<DbMigrationOptions> options,
            ILogger<DbMigrationService> logger)
        {
            _environmentRouting = environmentRouting;
            _loginContext = loginContext;
            _configuration = configuration;
            _options = options.Value;
            _logger = logger;
        }

        private int Timeout => _options.CommandTimeoutSeconds;
        private string Schema => EnsureValidName(_options.Schema, "schema");

        public async Task<DbSnapshotRecordDto> TakeSnapshotAsync(CreateDbSnapshotRequest request)
        {
            EnsureEnabled();
            await Gate.WaitAsync();
            try
            {
                await using var conn = await OpenAsync();
                return await SnapshotCoreAsync(conn, DbMigrationConstants.TYPE_MANUAL, request?.IncludeData ?? _options.IncludeData);
            }
            finally { Gate.Release(); }
        }

        public async Task<IEnumerable<DbSnapshotRecordDto>> ListAsync()
        {
            EnsureEnabled();
            await using var conn = await OpenAsync();
            return await ListCoreAsync(conn, null);
        }

        public async Task<DbSnapshotRecordDto> GetAsync(int snapshotId)
        {
            EnsureEnabled();
            await using var conn = await OpenAsync();
            return (await ListCoreAsync(conn, snapshotId)).FirstOrDefault()
                ?? throw new Exceptionlist.DataNotFoundException($"Snapshot {snapshotId} was not found.");
        }

        public async Task<DbMigrationReportDto> MigrateAsync(DbMigrateRequest request)
        {
            EnsureEnabled();
            var source = await ReadUploadAsync(request?.File);
            ApplyDatabaseMap(source, request!.DatabaseMap);

            await Gate.WaitAsync();
            try
            {
                await using var conn = await OpenAsync();
                var database = conn.Database;
                var target = await ReadAsync(conn, database, Schema, Timeout);
                var plan = SchemaDiffer.BuildPlan(source, target);
                var notSelected = SelectSteps(plan, request.Objects, request.Exclude);

                // Steps the existing rows would block (or be cut by), columns other objects depend on,
                // and procedures reading databases this server lacks go to manual review instead.
                var databaseExists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var step in plan.Steps.ToList())
                {
                    var reason = await DataConflictAsync(conn, step)
                        ?? await ColumnDependencyAsync(conn, step)
                        ?? await MissingDatabaseAsync(conn, database, step, databaseExists);
                    if (reason == null) continue;
                    plan.Steps.Remove(step);
                    plan.Manual.Add(new() { Object = step.Label, Reason = reason });
                }

                var report = new DbMigrationReportDto
                {
                    DryRun = request.DryRun ?? true,
                    SourceDatabase = source.Database,
                    TargetDatabase = database,
                    Manual = plan.Manual,
                    NotSelected = notSelected,
                    OnlyInTarget = plan.OnlyInTarget
                };
                var stepDtos = plan.Steps.ToDictionary(s => s, s => new DbMigrationStepDto { Kind = s.Kind, Object = s.Label, Status = STEP_PLANNED, Message = s.Change });
                report.Steps = stepDtos.Values.ToList();

                if (report.DryRun || plan.Steps.Count == 0)
                {
                    report.Success = true;
                    report.Summary = plan.Steps.Count == 0 ? "No table or procedure changes to apply." : "Dry run: " + StepSummary(plan.Steps);
                    return report;
                }

                var stamp = Stamp();
                var uploadPath = Path.Combine(EnsureFolder(), $"upload_{SafeName(database)}_{stamp}_{SafeName(request.File!.FileName)}");
                await using (var fs = File.Create(uploadPath)) await request.File.CopyToAsync(fs);

                await using (var tx = (SqlTransaction)await conn.BeginTransactionAsync())
                {
                    try
                    {
                        await ApplyAsync(conn, tx, plan.Steps, stepDtos);
                        await tx.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "DB migration failed on {Database}; rolling back the transaction.", database);
                        try { await tx.RollbackAsync(); }
                        catch (Exception rollbackEx) { _logger.LogError(rollbackEx, "Transaction rollback failed on {Database}.", database); }
                        foreach (var dto in stepDtos.Values)
                            dto.Status = dto.Status switch { STEP_APPLIED => STEP_ROLLED_BACK, STEP_PLANNED => STEP_NOT_RUN, _ => dto.Status };

                        report.Error = ex.Message;
                        report.Summary = $"Failed and rolled back: {ex.Message}";
                        report.MigrationId = await SaveRecordAsync(conn, DbMigrationConstants.TYPE_MIGRATION, DbMigrationConstants.STATUS_FAILED,
                            request.File.FileName, uploadPath, null, null, request.File.Length, report.Summary);
                        return report;
                    }
                }

                var rollbackPath = Path.Combine(EnsureFolder(), $"migration_{SafeName(database)}_{stamp}.json");
                await File.WriteAllTextAsync(rollbackPath, JsonSerializer.Serialize(new RollbackFile { Database = database, Steps = plan.Steps }));

                report.Success = true;
                report.Summary = StepSummary(plan.Steps) + (plan.Manual.Count > 0 ? $"; {plan.Manual.Count} left for manual review" : "");
                report.MigrationId = await SaveRecordAsync(conn, DbMigrationConstants.TYPE_MIGRATION, DbMigrationConstants.STATUS_APPLIED,
                    request.File.FileName, uploadPath, null, rollbackPath, request.File.Length, report.Summary);

                try
                {
                    report.PostSnapshot = await SnapshotCoreAsync(conn, DbMigrationConstants.TYPE_POST_MIGRATE, _options.IncludeData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Post-migration snapshot failed on {Database}.", database);
                    report.Error = "Migration applied, but the snapshot after it failed: " + ex.Message;
                }
                return report;
            }
            finally { Gate.Release(); }
        }

        public async Task<DbRollbackReportDto> RollbackAsync(int migrationId)
        {
            EnsureEnabled();
            await Gate.WaitAsync();
            try
            {
                await using var conn = await OpenAsync();
                var records = (await ListCoreAsync(conn, null)).ToList();
                var record = records.FirstOrDefault(r => r.SnapshotId == migrationId && r.SnapshotType == DbMigrationConstants.TYPE_MIGRATION)
                    ?? throw new Exceptionlist.DataNotFoundException($"Migration {migrationId} was not found.");
                if (record.Status != DbMigrationConstants.STATUS_APPLIED)
                    throw new Exceptionlist.InvalidDataException($"Migration {migrationId} is {record.Status}; only an APPLIED migration can be rolled back.");
                var later = records.FirstOrDefault(r => r.SnapshotType == DbMigrationConstants.TYPE_MIGRATION
                    && r.Status == DbMigrationConstants.STATUS_APPLIED && r.SnapshotId > migrationId);
                if (later != null)
                    throw new Exceptionlist.InvalidDataException($"Roll back the later migration {later.SnapshotId} first.");
                if (record.RollbackFilePath == null || !File.Exists(record.RollbackFilePath))
                    throw new Exceptionlist.InvalidDataException($"Rollback file for migration {migrationId} is missing: {record.RollbackFilePath}");

                var file = JsonSerializer.Deserialize<RollbackFile>(await File.ReadAllTextAsync(record.RollbackFilePath), JsonOptions)!;
                if (!string.Equals(file.Database, conn.Database, StringComparison.OrdinalIgnoreCase))
                    throw new Exceptionlist.InvalidDataException($"Migration {migrationId} was applied to {file.Database}, but this request is connected to {conn.Database}.");

                var stepDtos = file.Steps.ToDictionary(s => s, s => new DbMigrationStepDto { Kind = s.Kind, Object = s.Label, Status = STEP_APPLIED });
                await UndoAsync(conn, file.Steps, stepDtos);

                var steps = stepDtos.Values.ToList();
                var summary = $"Rolled back {steps.Count(s => s.Status == STEP_ROLLED_BACK)} of {steps.Count} step(s)"
                    + (steps.Any(s => s.Status == STEP_KEPT) ? $"; {steps.Count(s => s.Status == STEP_KEPT)} kept (see each step's message)" : "")
                    + (steps.Any(s => s.Status == STEP_FAILED) ? $"; {steps.Count(s => s.Status == STEP_FAILED)} failed" : "");
                await conn.ExecuteAsync(DbMigrationConstants.SP_SNAPSHOT_SET_STATUS, new
                {
                    P_SNAPSHOTID = migrationId,
                    P_STATUS = DbMigrationConstants.STATUS_ROLLED_BACK,
                    P_SUMMARY = Truncate(record.Summary + " | " + summary)
                }, commandType: CommandType.StoredProcedure);
                return new DbRollbackReportDto { MigrationId = migrationId, Summary = summary, Steps = steps };
            }
            finally { Gate.Release(); }
        }

        // ---------------------------------------------------------------- clone (live -> test)

        // Leading comments are kept in sys.sql_modules, so CREATE may come after them.
        private static readonly Regex CreateModuleHead = new(
            @"^(?<lead>(?:\s+|--[^\r\n]*(?:\r?\n|$)|/\*[\s\S]*?\*/)*)CREATE\s+(?:OR\s+ALTER\s+)?(?<kind>PROC(?:EDURE)?|VIEW|FUNCTION|TRIGGER)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// BACKUP ... COPY_ONLY of the source (the live backup chain is untouched), drop of the old target,
        /// RESTORE as the target with its files renamed, then modules naming the source DB are pointed at
        /// the target so the copy never reads or writes live. Runs on DefaultConnection's server via master.
        /// </summary>
        public async Task<DbCloneReportDto> CloneDatabaseAsync(DbCloneRequest request)
        {
            EnsureEnabled();
            request ??= new DbCloneRequest();

            var live = new SqlConnectionStringBuilder(_configuration.GetConnectionString("DefaultConnection"));
            var testConnection = _configuration.GetConnectionString("TestConnection");
            var test = string.IsNullOrWhiteSpace(testConnection) ? null : new SqlConnectionStringBuilder(testConnection);

            var source = EnsureValidName(string.IsNullOrWhiteSpace(request.SourceDatabase) ? live.InitialCatalog : request.SourceDatabase.Trim(), "source database");
            var target = EnsureValidName(string.IsNullOrWhiteSpace(request.TargetDatabase) ? test?.InitialCatalog : request.TargetDatabase.Trim(), "target database");

            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                throw new Exceptionlist.InvalidDataException("Source and target are the same database.");
            if (string.Equals(target, live.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                throw new Exceptionlist.ForbiddenException($"{target} is the live database (DefaultConnection) and is never overwritten.");
            var allowed = _options.CloneTargets.Append(test?.InitialCatalog).Where(n => !string.IsNullOrWhiteSpace(n));
            if (!allowed.Contains(target, StringComparer.OrdinalIgnoreCase))
                throw new Exceptionlist.ForbiddenException($"{target} is neither TestConnection's database nor listed in DbMigration:CloneTargets.");
            if (test != null && string.Equals(target, test.InitialCatalog, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(test.DataSource.Trim(), live.DataSource.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new Exceptionlist.InvalidDataException(
                    $"TestConnection is on {test.DataSource} but DefaultConnection on {live.DataSource}; the clone only works on one server.");

            var timeout = Math.Max(_options.CloneTimeoutSeconds, Timeout);
            var report = new DbCloneReportDto
            {
                DryRun = request.DryRun ?? true,
                Server = live.DataSource,
                SourceDatabase = source,
                TargetDatabase = target
            };

            await Gate.WaitAsync();
            try
            {
                await using var conn = new SqlConnection(new SqlConnectionStringBuilder(live.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
                await conn.OpenAsync();

                if (await conn.ExecuteScalarAsync<int?>("SELECT DB_ID(@source)", new { source }) == null)
                    throw new Exceptionlist.DataNotFoundException($"Database {source} does not exist on {live.DataSource}.");
                report.TargetExisted = await conn.ExecuteScalarAsync<int?>("SELECT DB_ID(@target)", new { target }) != null;
                if (report.TargetExisted && !request.Overwrite && !report.DryRun)
                    throw new Exceptionlist.InvalidDataException($"{target} already exists. Send Overwrite=true to drop it and replace it with a copy of {source}.");
                report.SourceSizeMb = await conn.ExecuteScalarAsync<long?>(
                    "SELECT SUM(CAST(size AS BIGINT)) * 8 / 1024 FROM sys.master_files WHERE database_id = DB_ID(@source)", new { source });

                var files = await CloneFilesAsync(conn, source, target);
                report.Files = files.Select(f => $"{f.LogicalName} -> {f.NewPath}").ToList();

                var backupFolder = !string.IsNullOrWhiteSpace(_options.CloneBackupPath)
                    ? _options.CloneBackupPath
                    : await conn.ExecuteScalarAsync<string?>("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(4000))");
                if (string.IsNullOrWhiteSpace(backupFolder))
                    throw new InvalidOperationException("Set DbMigration:CloneBackupPath to a folder on the SQL Server machine.");
                report.BackupFile = Path.Combine(backupFolder, $"{SafeName(source)}_to_{SafeName(target)}_{Stamp()}.bak");

                if (report.DryRun)
                {
                    if (request.RewriteReferences)
                        foreach (var m in await ModulesNamingAsync(conn, source, source))
                            report.RewrittenModules.Add(m.Label);
                    report.Success = true;
                    report.Summary = $"Dry run: copy {source} ({report.SourceSizeMb} MB) to {target}"
                        + (report.TargetExisted ? $"; {target} exists and will be DROPPED (needs Overwrite=true)" : "")
                        + (report.RewrittenModules.Count > 0 ? $"; {report.RewrittenModules.Count} module(s) name {source} and will point at {target}" : "");
                    return report;
                }

                var stage = "backup";
                try
                {
                    await conn.ExecuteAsync($"BACKUP DATABASE {Q(source)} TO DISK = @file WITH COPY_ONLY, INIT, CHECKSUM",
                        new { file = report.BackupFile }, commandTimeout: timeout);

                    if (report.TargetExisted)
                    {
                        stage = "drop of the old target";
                        await conn.ExecuteAsync($"ALTER DATABASE {Q(target)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Q(target)};",
                            commandTimeout: timeout);
                    }

                    stage = "restore";
                    var restore = new DynamicParameters();
                    restore.Add("file", report.BackupFile);
                    var moves = files.Select((f, i) =>
                    {
                        restore.Add($"l{i}", f.LogicalName);
                        restore.Add($"p{i}", f.NewPath);
                        return $"MOVE @l{i} TO @p{i}";
                    }).ToList();
                    await conn.ExecuteAsync($"RESTORE DATABASE {Q(target)} FROM DISK = @file WITH {string.Join(", ", moves)}, RECOVERY, CHECKSUM",
                        restore, commandTimeout: timeout);
                    // A test copy needs no log backups.
                    await conn.ExecuteAsync($"ALTER DATABASE {Q(target)} SET RECOVERY SIMPLE", commandTimeout: Timeout);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Clone {Source} -> {Target} failed during the {Stage}.", source, target, stage);
                    report.Error = ex.Message;
                    report.Summary = $"Failed during the {stage}: {ex.Message}"
                        + (stage == "restore" ? $" — {target} no longer exists; run the clone again." : "");
                    return report;
                }

                // Pooled connections still point at the dropped database, and its tracking SPs may differ now.
                SqlConnection.ClearAllPools();
                foreach (var key in TrackingReady.Keys.Where(k => k.EndsWith("|" + target, StringComparison.OrdinalIgnoreCase)))
                    TrackingReady.TryRemove(key, out _);

                await using var targetConn = new SqlConnection(new SqlConnectionStringBuilder(live.ConnectionString) { InitialCatalog = target }.ConnectionString);
                await targetConn.OpenAsync();

                if (request.RewriteReferences)
                    await RewriteReferencesAsync(targetConn, source, target, report);

                report.Success = true;
                report.Summary = $"Copied {source} ({report.SourceSizeMb} MB) to {target}"
                    + (report.RewrittenModules.Count > 0 ? $"; {report.RewrittenModules.Count} module(s) now point at {target}" : "")
                    + (report.RewriteFailures.Count > 0 ? $"; {report.RewriteFailures.Count} still name {source} — fix them manually" : "");

                await EnsureTrackingReadyAsync(targetConn);
                report.CloneId = await SaveRecordAsync(targetConn, DbMigrationConstants.TYPE_CLONE, DbMigrationConstants.STATUS_DONE,
                    $"{source} -> {target}", report.BackupFile, null, null, (report.SourceSizeMb ?? 0) * 1024 * 1024, report.Summary);
                return report;
            }
            finally { Gate.Release(); }
        }

        /// <summary>The source's files and where the target's copies go (source name in the file name replaced by the target's).</summary>
        private static async Task<List<CloneFile>> CloneFilesAsync(SqlConnection conn, string source, string target)
        {
            var dataFolder = await conn.ExecuteScalarAsync<string?>("SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(4000))");
            var logFolder = await conn.ExecuteScalarAsync<string?>("SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(4000))");

            var files = (await conn.QueryAsync<CloneFile>(@"
                SELECT name AS LogicalName, physical_name AS PhysicalName, type AS FileType
                FROM sys.master_files WHERE database_id = DB_ID(@source) ORDER BY file_id", new { source })).ToList();
            foreach (var f in files)
            {
                var folder = (f.FileType == 1 ? logFolder : dataFolder);
                if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(f.PhysicalName)!;
                var name = Path.GetFileName(f.PhysicalName);
                var renamed = Regex.Replace(name, Regex.Escape(source), target.Replace("$", "$$"), RegexOptions.IgnoreCase);
                f.NewPath = Path.Combine(folder, renamed == name ? $"{target}_{name}" : renamed);
            }

            // Never restore onto a file another database (live!) uses.
            var clash = await conn.ExecuteScalarAsync<string?>(@"
                SELECT TOP (1) DB_NAME(database_id) + N': ' + physical_name FROM sys.master_files
                WHERE physical_name IN @paths AND database_id <> ISNULL(DB_ID(@target), 0)",
                new { paths = files.Select(f => f.NewPath).ToList(), target });
            if (clash != null)
                throw new Exceptionlist.InvalidDataException($"The copy's file would overwrite {clash}.");
            return files;
        }

        /// <summary>Procedures, views, functions and triggers in <paramref name="database"/> whose text names <paramref name="name"/>.</summary>
        private async Task<List<ModuleRow>> ModulesNamingAsync(SqlConnection conn, string database, string name)
        {
            var db = Q(database);
            var rows = await conn.QueryAsync<ModuleRow>($@"
                SELECT s.name AS SchemaName, o.name AS Name, o.type_desc AS TypeDesc, m.definition AS Definition,
                       m.uses_ansi_nulls AS AnsiNulls, m.uses_quoted_identifier AS QuotedIdentifier
                FROM {db}.sys.sql_modules m
                JOIN {db}.sys.objects o ON o.object_id = m.object_id
                JOIN {db}.sys.schemas s ON s.schema_id = o.schema_id
                WHERE o.is_ms_shipped = 0 AND m.definition LIKE @pattern",
                new { pattern = "%" + name + "%" }, commandTimeout: Timeout);
            // LIKE treats _ as a wildcard: keep only real db.schema.object references.
            return rows.Where(r => SchemaDiffer.RewriteName(r.Definition, name, name + "\u0001") != r.Definition).ToList();
        }

        private async Task RewriteReferencesAsync(SqlConnection conn, string source, string target, DbCloneReportDto report)
        {
            foreach (var m in await ModulesNamingAsync(conn, target, source))
            {
                var rewritten = SchemaDiffer.RewriteName(m.Definition, source, target);
                if (!CreateModuleHead.IsMatch(rewritten))
                {
                    report.RewriteFailures.Add(new() { Object = m.Label, Reason = "no CREATE statement found at the start of its definition" });
                    continue;
                }
                var sql = CreateModuleHead.Replace(rewritten, "${lead}CREATE OR ALTER ${kind}", 1);
                var settings = m.AnsiNulls && m.QuotedIdentifier
                    ? null
                    : $"SET ANSI_NULLS {OnOff(m.AnsiNulls)}; SET QUOTED_IDENTIFIER {OnOff(m.QuotedIdentifier)};";
                try
                {
                    await ExecModuleAsync(conn, null, settings, sql);
                    report.RewrittenModules.Add(m.Label);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not point {Module} at {Target}.", m.Label, target);
                    report.RewriteFailures.Add(new() { Object = m.Label, Reason = ex.Message });
                }
            }

            var synonyms = await conn.QueryAsync<string>(
                "SELECT name + N' -> ' + base_object_name FROM sys.synonyms WHERE base_object_name LIKE @pattern",
                new { pattern = "%" + source + "%" }, commandTimeout: Timeout);
            foreach (var s in synonyms)
                report.RewriteFailures.Add(new() { Object = "SYNONYM " + s, Reason = $"may point at {source}; recreate it manually if so" });
        }

        private sealed class CloneFile
        {
            public string LogicalName { get; set; } = string.Empty;
            public string PhysicalName { get; set; } = string.Empty;
            public byte FileType { get; set; }               // 0 rows, 1 log, 2 filestream, 4 full-text
            public string NewPath { get; set; } = string.Empty;
        }

        private sealed class ModuleRow
        {
            public string SchemaName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string TypeDesc { get; set; } = string.Empty;
            public string Definition { get; set; } = string.Empty;
            public bool AnsiNulls { get; set; }
            public bool QuotedIdentifier { get; set; }
            public string Label => $"{TypeDesc} {SchemaName}.{Name}";
        }

        // ---------------------------------------------------------------- apply / undo

        private async Task ApplyAsync(SqlConnection conn, SqlTransaction tx, List<MigrationStep> steps,
            IReadOnlyDictionary<MigrationStep, DbMigrationStepDto> dtos)
        {
            foreach (var step in steps.Where(s => s.Kind is MigrationStep.CREATE_TABLE or MigrationStep.ADD_COLUMN or MigrationStep.ALTER_COLUMN))
                await RunStepAsync(conn, tx, step, dtos[step]);

            // Indexes, checks and foreign keys only once every new table exists (a FK may point at another
            // new table), and foreign keys last (they may need another new table's unique index).
            var post = steps.Where(s => s.PostSql?.Count > 0)
                .SelectMany(s => s.PostSql!.Select(sql => (Step: s, Sql: sql)))
                .OrderBy(p => IsForeignKey(p.Sql));
            foreach (var (step, sql) in post)
            {
                try { await conn.ExecuteAsync(sql, transaction: tx, commandTimeout: Timeout); }
                catch (Exception ex)
                {
                    dtos[step].Status = STEP_FAILED;
                    dtos[step].Message = ex.Message;
                    throw new InvalidOperationException($"{step.Kind} {step.Label}: {ex.Message}", ex);
                }
            }

            // SQL Server resolves names when a procedure runs, so one may reference another new one in any order.
            foreach (var step in steps.Where(s => s.Kind is MigrationStep.CREATE_PROCEDURE or MigrationStep.UPDATE_PROCEDURE))
            {
                try { await RunStepAsync(conn, tx, step, dtos[step]); }
                catch (Exception ex) { throw new InvalidOperationException($"{step.Kind} {step.Label}: {ex.Message}", ex); }
            }
        }

        private async Task<string?> DataConflictAsync(IDbConnection conn, MigrationStep step)
        {
            var column = step.Column == null ? null : Q(step.Column);
            if (step.NotNullWithoutDefault && await CountAsync(conn, Schema, step.Object, null, Timeout) > 0)
                return "NOT NULL column without a default on a table that has rows";
            if (step.BecomesNotNull && await CountAsync(conn, Schema, step.Object, $"{column} IS NULL", Timeout) > 0)
                return $"{step.Change}: the column has NULL values, so it cannot become NOT NULL";
            if (step.ShrinkFilter != null && await CountAsync(conn, Schema, step.Object, step.ShrinkFilter, Timeout) > 0)
                return $"{step.Change}: some values are longer than the new length — shorten them manually first";
            if (step.ConvertsType && await CountAsync(conn, Schema, step.Object, $"{column} IS NOT NULL", Timeout) > 0)
                return $"{step.Change}: changes the data type of a column that has values — convert it manually";
            return null;
        }

        /// <summary>SQL Server refuses ALTER COLUMN while an index, key, constraint or schema-bound object uses the column.</summary>
        private async Task<string?> ColumnDependencyAsync(IDbConnection conn, MigrationStep step)
        {
            if (step.Kind != MigrationStep.ALTER_COLUMN) return null;
            var dependency = await conn.ExecuteScalarAsync<string?>(@"
                DECLARE @id INT = OBJECT_ID(@table);
                DECLARE @col INT = COLUMNPROPERTY(@id, @column, 'ColumnId');
                SELECT TOP (1) d FROM (
                    SELECT N'index ' + i.name AS d
                    FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    WHERE ic.object_id = @id AND ic.column_id = @col
                    UNION ALL
                    SELECT N'foreign key ' + fk.name
                    FROM sys.foreign_key_columns fkc JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
                    WHERE (fkc.parent_object_id = @id AND fkc.parent_column_id = @col)
                       OR (fkc.referenced_object_id = @id AND fkc.referenced_column_id = @col)
                    UNION ALL
                    SELECT N'check constraint ' + cc.name FROM sys.check_constraints cc
                    WHERE cc.parent_object_id = @id AND cc.parent_column_id = @col
                    UNION ALL
                    SELECT N'schema-bound ' + OBJECT_NAME(d.referencing_id) FROM sys.sql_expression_dependencies d
                    WHERE d.referenced_id = @id AND d.referenced_minor_id = @col AND d.is_schema_bound_reference = 1
                ) x",
                new { table = $"{Q(Schema)}.{Q(step.Object)}", column = step.Column }, commandTimeout: Timeout);
            return dependency == null ? null : $"{step.Change}: used by {dependency} — alter it manually";
        }

        // db.schema.object (or db..object) inside a procedure body; 4-part linked-server names and method calls are skipped.
        private static readonly Regex ThreePartName = new(
            @"(?<![\w@#$\]\.])(?:\[(?<db>[^\]]+)\]|(?<db>[A-Za-z_][\w@#$]*))\s*\.\s*(?:\[[^\]]*\]|[A-Za-z_][\w@#$]*)?\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][\w@#$]*)(?!\s*[\.\(])",
            RegexOptions.Compiled);
        private static readonly Regex CommentsAndStrings = new(@"--[^\r\n]*|/\*[\s\S]*?\*/|N?'(?:[^']|'')*'", RegexOptions.Compiled);

        /// <summary>
        /// A procedure reading another database that is not on this server would only fail when it runs,
        /// so it goes to manual review. A first part that is a schema here is schema.table.column, not a database.
        /// </summary>
        private async Task<string?> MissingDatabaseAsync(IDbConnection conn, string database, MigrationStep step, Dictionary<string, bool> exists)
        {
            if (step.Kind is not (MigrationStep.CREATE_PROCEDURE or MigrationStep.UPDATE_PROCEDURE)) return null;

            foreach (Match m in ThreePartName.Matches(CommentsAndStrings.Replace(step.Sql, " ")))
            {
                var db = m.Groups["db"].Value;
                if (string.Equals(db, database, StringComparison.OrdinalIgnoreCase)) continue;
                if (!exists.TryGetValue(db, out var found))
                    exists[db] = found = await conn.ExecuteScalarAsync<int>(
                        "SELECT CASE WHEN DB_ID(@db) IS NOT NULL OR SCHEMA_ID(@db) IS NOT NULL THEN 1 ELSE 0 END",
                        new { db }, commandTimeout: Timeout) == 1;
                if (!found)
                    return $"reads database {db}, which does not exist on this server — set DatabaseMap (e.g. {db}=<database here>)";
            }
            return null;
        }

        /// <summary>Renames other databases referenced by the source DDL ("FROM=TO;FROM2=TO2").</summary>
        private static void ApplyDatabaseMap(SchemaSnapshot source, string? databaseMap)
        {
            if (string.IsNullOrWhiteSpace(databaseMap)) return;
            foreach (var pair in databaseMap.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = pair.Split('=', StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !ValidMapName(parts[0]) || !ValidMapName(parts[1]))
                    throw new Exceptionlist.InvalidDataException($"DatabaseMap entry '{pair}' must look like SOURCEDB=TARGETDB.");
                foreach (var o in source.Objects.Where(o => o.Ddl != null))
                    o.Ddl = SchemaDiffer.RewriteName(o.Ddl!, parts[0], parts[1]);
            }
        }

        /// <summary>
        /// Keeps only the steps named in Objects (all if empty), minus Exclude. A name matches a whole
        /// table/procedure or one Table.Column. Returns the labels left out; unknown names are an error.
        /// </summary>
        private static List<string> SelectSteps(MigrationPlan plan, string? objects, string? exclude)
        {
            static HashSet<string> Names(string? list) =>
                (list ?? string.Empty).Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(n => n.Replace("[", "").Replace("]", ""))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            static bool Matches(MigrationStep s, HashSet<string> names) => names.Contains(s.Object) || names.Contains(s.Label);

            var include = Names(objects);
            var leaveOut = Names(exclude);
            var unknown = include.Concat(leaveOut)
                .Where(n => !plan.Steps.Any(s => string.Equals(s.Object, n, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(s.Label, n, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (unknown.Count > 0)
                throw new Exceptionlist.InvalidDataException(
                    $"No planned change for: {string.Join(", ", unknown)}. Run a dry run without Objects/Exclude to see the step names.");

            var dropped = plan.Steps.Where(s => (include.Count > 0 && !Matches(s, include)) || Matches(s, leaveOut)).ToList();
            foreach (var s in dropped) plan.Steps.Remove(s);
            return dropped.Select(s => s.Label).ToList();
        }

        private static bool ValidMapName(string name) => Regex.IsMatch(name, @"^[A-Za-z0-9_\-$#@]+$");

        private async Task RunStepAsync(SqlConnection conn, SqlTransaction? tx, MigrationStep step, DbMigrationStepDto dto)
        {
            try
            {
                await ExecModuleAsync(conn, tx, step.Settings, step.Sql);
                dto.Status = STEP_APPLIED;
            }
            catch (Exception ex)
            {
                dto.Status = STEP_FAILED;
                dto.Message = ex.Message;
                throw;
            }
        }

        /// <summary>Runs <paramref name="sql"/> under the given SET options (a procedure keeps the ones it was created with).</summary>
        private async Task ExecModuleAsync(SqlConnection conn, SqlTransaction? tx, string? settings, string sql)
        {
            if (settings != null) await conn.ExecuteAsync(settings, transaction: tx, commandTimeout: Timeout);
            try { await conn.ExecuteAsync(sql, transaction: tx, commandTimeout: Timeout); }
            finally
            {
                if (settings != null)
                {
                    try { await conn.ExecuteAsync(DEFAULT_SETTINGS, transaction: tx, commandTimeout: Timeout); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Could not reset SET options."); }
                }
            }
        }

        private string DropColumnSql(string table, string column) => $@"
            DECLARE @df SYSNAME = (SELECT dc.name FROM sys.default_constraints dc
                JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID({Str($"{Q(Schema)}.{Q(table)}")}) AND c.name = {Str(column)});
            IF @df IS NOT NULL EXEC (N'ALTER TABLE {Q(Schema).Replace("'", "''")}.{Q(table).Replace("'", "''")} DROP CONSTRAINT ' + QUOTENAME(@df));
            ALTER TABLE {Q(Schema)}.{Q(table)} DROP COLUMN {Q(column)};";

        /// <summary>
        /// Reverses an applied migration newest first. Restores/drops procedures; drops a created
        /// table or added column only when it holds no data.
        /// </summary>
        private async Task UndoAsync(SqlConnection conn, IList<MigrationStep> applied, IReadOnlyDictionary<MigrationStep, DbMigrationStepDto> dtos)
        {
            var blockedDrops = new List<(MigrationStep Step, string Sql, Exception Error)>();
            foreach (var step in applied.Reverse())
            {
                var dto = dtos[step];
                try
                {
                    string? sql = step.Kind switch
                    {
                        MigrationStep.UPDATE_PROCEDURE => step.PreviousDdl,
                        MigrationStep.CREATE_PROCEDURE => $"DROP PROCEDURE {Q(Schema)}.{Q(step.Object)}",
                        MigrationStep.ALTER_COLUMN => step.PreviousDdl,
                        MigrationStep.ADD_COLUMN when await CountAsync(conn, Schema, step.Object, $"{Q(step.Column!)} IS NOT NULL", Timeout) == 0
                            => DropColumnSql(step.Object, step.Column!),
                        MigrationStep.CREATE_TABLE when await CountAsync(conn, Schema, step.Object, null, Timeout) == 0
                            => $"DROP TABLE {Q(Schema)}.{Q(step.Object)}",
                        _ => null
                    };
                    if (sql == null)
                    {
                        dto.Status = STEP_KEPT;
                        dto.Message = "Holds data, so it was not dropped. Drop it manually if it is no longer needed.";
                        continue;
                    }
                    try
                    {
                        await ExecModuleAsync(conn, null, step.Kind == MigrationStep.UPDATE_PROCEDURE ? step.PreviousSettings : null, sql);
                    }
                    catch (Exception ex) when (step.Kind == MigrationStep.CREATE_TABLE)
                    {
                        // Probably still referenced by another new table's foreign key: retry after the others.
                        blockedDrops.Add((step, sql, ex));
                        continue;
                    }
                    dto.Status = STEP_ROLLED_BACK;
                }
                catch (Exception ex) when (step.Kind == MigrationStep.ALTER_COLUMN)
                {
                    // e.g. values written since no longer fit the old type: the new type stays.
                    dto.Status = STEP_KEPT;
                    dto.Message = $"Old type could not be restored ({ex.Message}); the new type is kept, no data changed.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Undo of {Kind} {Object} failed.", step.Kind, step.Label);
                    dto.Status = STEP_FAILED;
                    dto.Message = "Rollback failed: " + ex.Message;
                }
            }

            while (blockedDrops.Count > 0)
            {
                var stillBlocked = new List<(MigrationStep Step, string Sql, Exception Error)>();
                foreach (var drop in blockedDrops)
                {
                    try
                    {
                        await conn.ExecuteAsync(drop.Sql, commandTimeout: Timeout);
                        dtos[drop.Step].Status = STEP_ROLLED_BACK;
                    }
                    catch (Exception ex) { stillBlocked.Add((drop.Step, drop.Sql, ex)); }
                }
                if (stillBlocked.Count == blockedDrops.Count)
                {
                    foreach (var (step, _, error) in stillBlocked)
                    {
                        _logger.LogError(error, "Undo of {Kind} {Object} failed.", step.Kind, step.Label);
                        dtos[step].Status = STEP_FAILED;
                        dtos[step].Message = "Rollback failed: " + error.Message;
                    }
                    break;
                }
                blockedDrops = stillBlocked;
            }
        }

        // ---------------------------------------------------------------- snapshot

        private async Task<DbSnapshotRecordDto> SnapshotCoreAsync(SqlConnection conn, string type, bool includeData)
        {
            var snapshot = await ReadAsync(conn, conn.Database, Schema, Timeout);
            var baseName = $"snap_{SafeName(conn.Database)}_{Stamp()}";
            var folder = EnsureFolder();
            var jsonPath = Path.Combine(folder, baseName + ".json");
            var sqlPath = Path.Combine(folder, baseName + ".sql");

            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(snapshot));
            var rows = await WriteBackupSqlAsync(conn, snapshot, sqlPath, includeData, Timeout);

            var counts = snapshot.Objects.GroupBy(o => o.Type).Select(g => $"{g.Count()} {g.Key.ToLowerInvariant()}");
            var summary = string.Join(", ", counts) + (includeData ? $"; {rows} rows" : "; structure only");
            var size = new FileInfo(jsonPath).Length + new FileInfo(sqlPath).Length;

            var id = await SaveRecordAsync(conn, type, DbMigrationConstants.STATUS_DONE, baseName, jsonPath, sqlPath, null, size, summary);
            return (await ListCoreAsync(conn, id)).First();
        }

        private static async Task<SchemaSnapshot> ReadUploadAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                throw new Exceptionlist.InvalidDataException("A snapshot file (.json from the snapshot endpoint) is required.");
            try
            {
                await using var stream = file.OpenReadStream();
                var snapshot = await JsonSerializer.DeserializeAsync<SchemaSnapshot>(stream, JsonOptions);
                if (snapshot?.FormatVersion != DbMigrationConstants.SNAPSHOT_FORMAT_VERSION || snapshot.Objects.Count == 0
                    || string.IsNullOrEmpty(snapshot.Database))
                    throw new Exceptionlist.InvalidDataException("This is not a structure snapshot (.json) created by the snapshot endpoint.");
                EnsureValidName(snapshot.Schema, "schema");
                return snapshot;
            }
            catch (JsonException)
            {
                throw new Exceptionlist.InvalidDataException("Upload the .json structure snapshot, not the .sql backup.");
            }
        }

        // ---------------------------------------------------------------- tracking table

        private static async Task<IEnumerable<DbSnapshotRecordDto>> ListCoreAsync(SqlConnection conn, int? snapshotId) =>
            await conn.QueryAsync<DbSnapshotRecordDto>(DbMigrationConstants.SP_SNAPSHOT_LIST,
                new { P_SNAPSHOTID = snapshotId }, commandType: CommandType.StoredProcedure);

        private async Task<int> SaveRecordAsync(SqlConnection conn, string type, string status, string? fileName, string? filePath,
            string? sqlFilePath, string? rollbackFilePath, long? sizeBytes, string summary)
        {
            return await conn.QuerySingleAsync<int>(DbMigrationConstants.SP_SNAPSHOT_SAVE, new
            {
                P_SNAPSHOTTYPE = type,
                P_STATUS = status,
                P_FILENAME = fileName,
                P_FILEPATH = filePath,
                P_SQLFILEPATH = sqlFilePath,
                P_ROLLBACKFILEPATH = rollbackFilePath,
                P_SIZEBYTES = sizeBytes,
                P_SUMMARY = Truncate(summary),
                P_CREATEDBY = _loginContext.userName ?? _loginContext.userId.ToString()
            }, commandType: CommandType.StoredProcedure);
        }

        // Creates SYS_DBSNAPSHOT and its SPs from the embedded DbSnapshot.sql the first time per database.
        private async Task EnsureTrackingReadyAsync(SqlConnection conn)
        {
            var key = conn.DataSource + "|" + conn.Database;
            if (TrackingReady.ContainsKey(key)) return;
            var exists = await conn.ExecuteScalarAsync<int?>(
                "SELECT OBJECT_ID(@name, N'P')", new { name = DbMigrationConstants.SP_SNAPSHOT_SET_STATUS });
            if (exists == null)
            {
                _logger.LogInformation("Creating {Table} and its procedures in {Database}.", DbMigrationConstants.TRACKING_TABLE, conn.Database);
                using var stream = typeof(DbMigrationService).Assembly.GetManifestResourceStream(DbMigrationConstants.BOOTSTRAP_RESOURCE)
                    ?? throw new InvalidOperationException($"Embedded resource {DbMigrationConstants.BOOTSTRAP_RESOURCE} is missing.");
                using var reader = new StreamReader(stream);
                foreach (var batch in SplitBatches(await reader.ReadToEndAsync()))
                    await conn.ExecuteAsync(batch, commandTimeout: Timeout);
            }
            TrackingReady[key] = true;
        }

        // ---------------------------------------------------------------- helpers

        private void EnsureEnabled()
        {
            if (_loginContext.role != AppRoles.Admin)
                throw new Exceptionlist.ForbiddenException("Only an administrator can use the DB snapshot/migration endpoints.");
            if (!_options.Enabled)
                throw new Exceptionlist.ForbiddenException("DB snapshot/migration endpoints are disabled (DbMigration:Enabled).");
            if (string.IsNullOrWhiteSpace(_options.SnapshotPath))
                throw new InvalidOperationException("DbMigration:SnapshotPath is not configured.");
        }

        // The DB named by the connection string (DefaultConnection, or TestConnection with X-Environment: Test).
        // Not IDbConnectionFactory: it swaps in the login's DbName claim, so every token would hit the same DB.
        private async Task<SqlConnection> OpenAsync()
        {
            var conn = new SqlConnection(_environmentRouting.GetBaseConnectionString());
            try
            {
                await conn.OpenAsync();
                await EnsureTrackingReadyAsync(conn);
                return conn;
            }
            catch
            {
                await conn.DisposeAsync();
                throw;
            }
        }

        private string EnsureFolder() => Directory.CreateDirectory(_options.SnapshotPath).FullName;

        private static string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss");

        private static string SafeName(string fileName) =>
            Regex.Replace(Path.GetFileName(fileName), @"[^A-Za-z0-9_.-]", "_");

        private static string Truncate(string text) => text.Length <= 5000 ? text : text[..4997] + "...";

        private static string StepSummary(IEnumerable<MigrationStep> steps) =>
            string.Join(", ", steps.GroupBy(s => s.Kind).Select(g => $"{g.Count()} {g.Key.ToLowerInvariant().Replace('_', ' ')}"));

        private sealed class RollbackFile
        {
            public string Database { get; set; } = string.Empty;
            public List<MigrationStep> Steps { get; set; } = new();
        }
    }
}
