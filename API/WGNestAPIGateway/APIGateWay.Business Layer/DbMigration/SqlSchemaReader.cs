using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;

namespace APIGateWay.Business_Layer.DbMigration
{
    /// <summary>Structure of one schema in one database: every object's DDL, plus column metadata for tables.</summary>
    public class SchemaSnapshot
    {
        public int FormatVersion { get; set; }
        public string Database { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public DateTime TakenAt { get; set; }
        public List<SchemaObject> Objects { get; set; } = new();
    }

    public class SchemaObject
    {
        public string Type { get; set; } = string.Empty;   // TABLE | VIEW | SEQUENCE | FUNCTION | PROCEDURE | TRIGGER
        public string Name { get; set; } = string.Empty;
        public string? Ddl { get; set; }                   // TABLE: CREATE TABLE + its index/constraint statements, GO-separated
        public bool? AnsiNulls { get; set; }               // modules: SET options the object was created with
        public bool? QuotedIdentifier { get; set; }
        public List<ColumnSchema>? Columns { get; set; }  // TABLE only
    }

    public class ColumnSchema
    {
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public string DataType { get; set; } = string.Empty; // sys.types name, or [schema].[name] for a user-defined type
        public int? Length { get; set; }                     // characters for (n)char/(n)varchar, bytes for binary; -1 = MAX
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultValue { get; set; }            // default constraint definition, e.g. ((0)) or (getdate())
        public string? DefaultName { get; set; }             // only when the constraint was named explicitly
        public string? GenerationType { get; set; }          // IDENTITY(seed,increment) or COMPUTED when set
        public string? ComputedDefinition { get; set; }
        public bool IsPersisted { get; set; }
    }

    /// <summary>
    /// Reads SQL Server catalog metadata, scripts tables, and writes the .sql backup. Object names come
    /// from the catalog and are inlined bracket-quoted; DDL cannot take parameters anyway.
    /// </summary>
    internal static class SqlSchemaReader
    {
        public const string TABLE = "TABLE";
        public const string PROCEDURE = "PROCEDURE";
        public const string GO = "\nGO\n";

        private static readonly string[] ObjectTypesInCreateOrder =
            { "SEQUENCE", TABLE, "VIEW", "FUNCTION", PROCEDURE, "TRIGGER" };

        private static readonly Regex ValidName = new(@"^[A-Za-z0-9_\-$#@]+$", RegexOptions.Compiled);
        private static readonly Regex GoLine = new(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Types the backup cannot write as a literal / cannot insert into.
        private static readonly HashSet<string> NotInsertableTypes = new(StringComparer.OrdinalIgnoreCase) { "timestamp", "rowversion" };
        private static readonly HashSet<string> ClrTypes = new(StringComparer.OrdinalIgnoreCase) { "geography", "geometry", "hierarchyid" };

        public static string Q(string name) => "[" + name.Replace("]", "]]") + "]";
        public static string Str(string value) => "N'" + value.Replace("'", "''") + "'";

        public static string EnsureValidName(string? name, string what) =>
            !string.IsNullOrEmpty(name) && ValidName.IsMatch(name)
                ? name
                : throw new InvalidOperationException($"Invalid {what} name '{name}'.");

        /// <summary>Splits a GO-separated script into batches.</summary>
        public static IEnumerable<string> SplitBatches(string script) =>
            GoLine.Split(script).Select(s => s.Trim()).Where(s => s.Length > 0);

        public static async Task<SchemaSnapshot> ReadAsync(IDbConnection conn, string database, string schema, int timeout)
        {
            var p = new { schema };
            var objects = (await conn.QueryAsync<ObjectRow>(@"
                SELECT o.name AS Name,
                       CASE o.type WHEN 'U' THEN 'TABLE' WHEN 'V' THEN 'VIEW' WHEN 'P' THEN 'PROCEDURE'
                                   WHEN 'TR' THEN 'TRIGGER' WHEN 'SO' THEN 'SEQUENCE' ELSE 'FUNCTION' END AS Type,
                       m.definition AS Definition, m.uses_ansi_nulls AS AnsiNulls, m.uses_quoted_identifier AS QuotedIdentifier
                FROM sys.objects o
                LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
                WHERE o.schema_id = SCHEMA_ID(@schema) AND o.is_ms_shipped = 0
                  AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF', 'TR', 'SO')
                  AND NOT EXISTS (SELECT 1 FROM sys.extended_properties ep
                                  WHERE ep.class = 1 AND ep.major_id = o.object_id AND ep.minor_id = 0
                                    AND ep.name = N'microsoft_database_tools_support')", p, commandTimeout: timeout)).ToList();

            var columns = (await conn.QueryAsync<ColumnRow>(@"
                SELECT t.name AS TableName, c.name AS Name, c.column_id AS Position,
                       ty.name AS DataType, ty.is_user_defined AS IsUserType, SCHEMA_NAME(ty.schema_id) AS TypeSchema,
                       CAST(CASE WHEN ty.name IN ('nchar', 'nvarchar') AND c.max_length > 0 THEN c.max_length / 2 ELSE c.max_length END AS INT) AS Length,
                       CAST(c.precision AS INT) AS [Precision], CAST(c.scale AS INT) AS Scale,
                       c.is_nullable AS IsNullable, c.is_identity AS IsIdentity,
                       CAST(ic.seed_value AS NVARCHAR(40)) AS IdentitySeed, CAST(ic.increment_value AS NVARCHAR(40)) AS IdentityIncrement,
                       dc.definition AS DefaultValue, dc.name AS DefaultName, dc.is_system_named AS DefaultSystemNamed,
                       cc.definition AS ComputedDefinition, ISNULL(cc.is_persisted, 0) AS IsPersisted
                FROM sys.tables t
                JOIN sys.columns c ON c.object_id = t.object_id
                JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
                WHERE t.schema_id = SCHEMA_ID(@schema) AND t.is_ms_shipped = 0
                ORDER BY t.name, c.column_id", p, commandTimeout: timeout))
                .GroupBy(c => c.TableName)
                .ToDictionary(g => g.Key, g => g.Select(c => c.ToColumn()).ToList());

            var indexes = (await conn.QueryAsync<IndexRow>(@"
                SELECT t.name AS TableName, i.name AS IndexName, i.index_id AS IndexId, i.type_desc AS TypeDesc,
                       i.is_unique AS IsUnique, i.is_primary_key AS IsPrimaryKey, i.is_unique_constraint AS IsUniqueConstraint,
                       ISNULL(kc.is_system_named, 0) AS ConstraintSystemNamed,
                       i.has_filter AS HasFilter, i.filter_definition AS FilterDefinition,
                       c.name AS ColumnName, CAST(ic.key_ordinal AS INT) AS KeyOrdinal,
                       ic.is_descending_key AS IsDescending, ic.is_included_column AS IsIncluded
                FROM sys.tables t
                JOIN sys.indexes i ON i.object_id = t.object_id
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = i.object_id AND kc.unique_index_id = i.index_id
                WHERE t.schema_id = SCHEMA_ID(@schema) AND t.is_ms_shipped = 0 AND i.type IN (1, 2) AND i.is_hypothetical = 0
                ORDER BY t.name, i.index_id, ic.key_ordinal, ic.index_column_id", p, commandTimeout: timeout))
                .ToLookup(i => i.TableName);

            var foreignKeys = (await conn.QueryAsync<ForeignKeyRow>(@"
                SELECT pt.name AS TableName, fk.name AS Name, fk.is_system_named AS IsSystemNamed,
                       SCHEMA_NAME(rt.schema_id) AS RefSchema, rt.name AS RefTable,
                       pc.name AS ColumnName, rc.name AS RefColumnName,
                       fk.delete_referential_action_desc AS OnDelete, fk.update_referential_action_desc AS OnUpdate
                FROM sys.foreign_keys fk
                JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
                JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
                JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
                JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
                WHERE pt.schema_id = SCHEMA_ID(@schema)
                ORDER BY pt.name, fk.name, fkc.constraint_column_id", p, commandTimeout: timeout))
                .ToLookup(f => f.TableName);

            var checks = (await conn.QueryAsync<CheckRow>(@"
                SELECT t.name AS TableName, cc.name AS Name, cc.is_system_named AS IsSystemNamed, cc.definition AS Definition
                FROM sys.check_constraints cc
                JOIN sys.tables t ON t.object_id = cc.parent_object_id
                WHERE t.schema_id = SCHEMA_ID(@schema)
                ORDER BY t.name, cc.name", p, commandTimeout: timeout))
                .ToLookup(c => c.TableName);

            var sequences = (await conn.QueryAsync<SequenceRow>(@"
                SELECT s.name AS Name, TYPE_NAME(s.user_type_id) AS DataType,
                       CAST(s.start_value AS NVARCHAR(40)) AS StartValue, CAST(s.increment AS NVARCHAR(40)) AS Increment,
                       CAST(s.minimum_value AS NVARCHAR(40)) AS MinValue, CAST(s.maximum_value AS NVARCHAR(40)) AS MaxValue,
                       s.is_cycling AS IsCycling
                FROM sys.sequences s WHERE s.schema_id = SCHEMA_ID(@schema)", p, commandTimeout: timeout))
                .ToDictionary(s => s.Name);

            var snapshot = new SchemaSnapshot
            {
                FormatVersion = DbMigrationConstants.SNAPSHOT_FORMAT_VERSION,
                Database = database,
                Schema = schema,
                TakenAt = DateTime.UtcNow
            };

            foreach (var o in objects.OrderBy(o => Array.IndexOf(ObjectTypesInCreateOrder, o.Type)).ThenBy(o => o.Name, StringComparer.Ordinal))
            {
                var cols = o.Type == TABLE && columns.TryGetValue(o.Name, out var c) ? c : null;
                snapshot.Objects.Add(new SchemaObject
                {
                    Type = o.Type,
                    Name = o.Name,
                    Ddl = o.Type switch
                    {
                        TABLE => cols == null ? null : ScriptTable(schema, o.Name, cols, indexes[o.Name], foreignKeys[o.Name], checks[o.Name]),
                        "SEQUENCE" => sequences.TryGetValue(o.Name, out var s) ? ScriptSequence(schema, s) : null,
                        _ => o.Definition?.Trim()   // NULL for an encrypted module
                    },
                    AnsiNulls = o.Definition == null ? null : o.AnsiNulls,
                    QuotedIdentifier = o.Definition == null ? null : o.QuotedIdentifier,
                    Columns = cols
                });
            }
            return snapshot;
        }

        /// <summary>CREATE TABLE with its primary key / unique constraints, then indexes, checks and foreign keys.</summary>
        private static string ScriptTable(string schema, string table, List<ColumnSchema> columns,
            IEnumerable<IndexRow> indexes, IEnumerable<ForeignKeyRow> foreignKeys, IEnumerable<CheckRow> checks)
        {
            var target = $"{Q(schema)}.{Q(table)}";
            var lines = columns.Select(c => "    " + ColumnDefinition(c)).ToList();
            var post = new List<string>();

            foreach (var g in indexes.GroupBy(i => i.IndexId).Select(g => g.ToList()))
            {
                var i = g[0];
                var keys = string.Join(", ", g.Where(c => !c.IsIncluded && c.KeyOrdinal > 0).OrderBy(c => c.KeyOrdinal)
                    .Select(c => Q(c.ColumnName) + (c.IsDescending ? " DESC" : " ASC")));
                if (i.IsPrimaryKey || i.IsUniqueConstraint)
                {
                    var name = i.ConstraintSystemNamed ? "" : $"CONSTRAINT {Q(i.IndexName)} ";
                    lines.Add($"    {name}{(i.IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE")} {i.TypeDesc} ({keys})");
                    continue;
                }
                var include = g.Where(c => c.IsIncluded).Select(c => Q(c.ColumnName)).ToList();
                post.Add($"CREATE {(i.IsUnique ? "UNIQUE " : "")}{i.TypeDesc} INDEX {Q(i.IndexName)} ON {target} ({keys})"
                    + (include.Count > 0 ? $" INCLUDE ({string.Join(", ", include)})" : "")
                    + (i.HasFilter ? $" WHERE {i.FilterDefinition}" : ""));
            }

            foreach (var c in checks)
                post.Add($"ALTER TABLE {target} ADD {(c.IsSystemNamed ? "" : $"CONSTRAINT {Q(c.Name)} ")}CHECK {c.Definition}");

            foreach (var g in foreignKeys.GroupBy(f => f.Name).Select(g => g.ToList()))
            {
                var f = g[0];
                var sql = $"ALTER TABLE {target} ADD {(f.IsSystemNamed ? "" : $"CONSTRAINT {Q(f.Name)} ")}FOREIGN KEY ("
                    + string.Join(", ", g.Select(x => Q(x.ColumnName))) + $") REFERENCES {Q(f.RefSchema)}.{Q(f.RefTable)} ("
                    + string.Join(", ", g.Select(x => Q(x.RefColumnName))) + ")";
                if (f.OnDelete != "NO_ACTION") sql += " ON DELETE " + f.OnDelete.Replace('_', ' ');
                if (f.OnUpdate != "NO_ACTION") sql += " ON UPDATE " + f.OnUpdate.Replace('_', ' ');
                post.Add(sql);
            }

            return $"CREATE TABLE {target} (\n{string.Join(",\n", lines)}\n)" + string.Concat(post.Select(s => GO + s));
        }

        private static string ScriptSequence(string schema, SequenceRow s) =>
            $"CREATE SEQUENCE {Q(schema)}.{Q(s.Name)} AS {s.DataType} START WITH {s.StartValue} INCREMENT BY {s.Increment} " +
            $"MINVALUE {s.MinValue} MAXVALUE {s.MaxValue} {(s.IsCycling ? "CYCLE" : "NO CYCLE")}";

        public static bool IsForeignKey(string sql) => sql.Contains(" FOREIGN KEY (", StringComparison.Ordinal);

        public static string TypeSpec(ColumnSchema c)
        {
            var type = c.DataType.ToLowerInvariant();
            return type switch
            {
                "varchar" or "nvarchar" or "char" or "nchar" or "varbinary" or "binary"
                    => $"{type}({(c.Length == -1 ? "max" : c.Length?.ToString(CultureInfo.InvariantCulture))})",
                "decimal" or "numeric" => $"{type}({c.Precision},{c.Scale})",
                "datetime2" or "time" or "datetimeoffset" => $"{type}({c.Scale})",
                "float" => c.Precision is null or 53 ? type : $"float({c.Precision})",
                _ => c.DataType   // fixed-size types and [schema].[user type]
            };
        }

        public static string ColumnDefinition(ColumnSchema c)
        {
            if (c.ComputedDefinition != null)
                return $"{Q(c.Name)} AS {c.ComputedDefinition}" + (c.IsPersisted ? " PERSISTED" + (c.IsNullable ? "" : " NOT NULL") : "");

            var def = $"{Q(c.Name)} {TypeSpec(c)}";
            if (c.GenerationType != null) def += " " + c.GenerationType;
            if (c.DefaultValue != null)
                def += (c.DefaultName != null ? $" CONSTRAINT {Q(c.DefaultName)}" : "") + " DEFAULT " + c.DefaultValue;
            return def + (c.IsNullable ? " NULL" : " NOT NULL");
        }

        /// <summary>Row count, optionally filtered by <paramref name="where"/> (built from quoted names only).</summary>
        public static async Task<long> CountAsync(IDbConnection conn, string schema, string table, string? where, int timeout)
        {
            var filter = where == null ? "" : " WHERE " + where;
            return await conn.ExecuteScalarAsync<long>($"SELECT COUNT_BIG(*) FROM {Q(schema)}.{Q(table)}{filter}", commandTimeout: timeout);
        }

        /// <summary>Full backup: every object's DDL, then (optionally) every table's rows as INSERTs. Returns row count.</summary>
        public static async Task<long> WriteBackupSqlAsync(IDbConnection conn, SchemaSnapshot snapshot, string path, bool includeData, int timeout)
        {
            await using var w = new StreamWriter(path, false, new UTF8Encoding(false));
            await w.WriteLineAsync($"-- SQL Server snapshot of database {snapshot.Database} (schema {snapshot.Schema}) taken {snapshot.TakenAt:yyyy-MM-dd HH:mm:ss} UTC");
            await w.WriteLineAsync("-- Objects first (DDL), then table data (INSERTs). A backup for manual restore, not a migration.");
            await w.WriteLineAsync("-- Run against an empty database in SSMS / sqlcmd (batches are separated by GO).");
            await w.WriteLineAsync();

            foreach (var o in snapshot.Objects)
            {
                if (o.Ddl == null) { await w.WriteAsync($"-- {o.Type} {o.Name}: DDL NOT CAPTURED\n\n"); continue; }
                await w.WriteLineAsync($"-- {o.Type} {o.Name}");
                if (o.AnsiNulls != null)
                    await w.WriteAsync($"SET ANSI_NULLS {OnOff(o.AnsiNulls)}{GO}SET QUOTED_IDENTIFIER {OnOff(o.QuotedIdentifier)}{GO}");
                await w.WriteAsync(o.Ddl + GO + "\n");
            }

            long total = 0;
            if (includeData)
            {
                await w.WriteLineAsync("SET ANSI_NULLS ON" + GO + "SET QUOTED_IDENTIFIER ON" + GO);
                foreach (var table in snapshot.Objects.Where(o => o.Type == TABLE && o.Columns != null))
                {
                    var target = $"{Q(snapshot.Schema)}.{Q(table.Name)}";
                    var cols = table.Columns!.Where(c => c.ComputedDefinition == null && !NotInsertableTypes.Contains(c.DataType)).ToList();
                    if (cols.Count == 0) continue;
                    var hasIdentity = cols.Any(c => c.GenerationType != null);
                    var select = string.Join(", ", cols.Select(c => ClrTypes.Contains(c.DataType)
                        ? $"CAST({Q(c.Name)} AS VARBINARY(MAX)) AS {Q(c.Name)}"
                        : Q(c.Name)));

                    using var reader = await conn.ExecuteReaderAsync($"SELECT {select} FROM {target}", commandTimeout: timeout);
                    string? insert = null;
                    while (reader.Read())
                    {
                        if (insert == null)
                        {
                            insert = $"INSERT INTO {target} ({string.Join(", ", cols.Select(c => Q(c.Name)))}) VALUES (";
                            await w.WriteLineAsync($"-- DATA {table.Name}");
                            if (hasIdentity) await w.WriteLineAsync($"SET IDENTITY_INSERT {target} ON;");
                        }
                        var values = Enumerable.Range(0, reader.FieldCount).Select(i => Literal(reader.GetValue(i)));
                        await w.WriteLineAsync(insert + string.Join(", ", values) + ");");
                        total++;
                    }
                    if (insert != null)
                    {
                        if (hasIdentity) await w.WriteLineAsync($"SET IDENTITY_INSERT {target} OFF;");
                        await w.WriteAsync(GO + "\n");
                    }
                }
            }
            await w.WriteLineAsync("-- END OF SNAPSHOT");
            return total;
        }

        public static string OnOff(bool? value) => value == false ? "OFF" : "ON";

        private static string Literal(object? v) => v switch
        {
            null or DBNull => "NULL",
            byte[] b => "0x" + Convert.ToHexString(b),
            DateTime d => $"CAST('{d.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)}' AS DATETIME2)",
            DateTimeOffset d => $"CAST('{d.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)}' AS DATETIMEOFFSET)",
            TimeSpan t => $"'{t.ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture)}'",
            bool b => b ? "1" : "0",
            Guid g => $"'{g}'",
            string s => Str(s),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            sbyte or byte or short or ushort or int or uint or long or ulong or decimal =>
                Convert.ToString(v, CultureInfo.InvariantCulture)!,
            _ => Str(Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty)
        };

        private sealed class ObjectRow
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string? Definition { get; set; }
            public bool? AnsiNulls { get; set; }
            public bool? QuotedIdentifier { get; set; }
        }

        private sealed class ColumnRow
        {
            public string TableName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Position { get; set; }
            public string DataType { get; set; } = string.Empty;
            public bool IsUserType { get; set; }
            public string? TypeSchema { get; set; }
            public int? Length { get; set; }
            public int? Precision { get; set; }
            public int? Scale { get; set; }
            public bool IsNullable { get; set; }
            public bool IsIdentity { get; set; }
            public string? IdentitySeed { get; set; }
            public string? IdentityIncrement { get; set; }
            public string? DefaultValue { get; set; }
            public string? DefaultName { get; set; }
            public bool? DefaultSystemNamed { get; set; }
            public string? ComputedDefinition { get; set; }
            public bool IsPersisted { get; set; }

            public ColumnSchema ToColumn() => new()
            {
                Name = Name,
                Position = Position,
                DataType = IsUserType ? $"{Q(TypeSchema ?? "dbo")}.{Q(DataType)}" : DataType,
                Length = Length,
                Precision = Precision,
                Scale = Scale,
                IsNullable = IsNullable,
                DefaultValue = DefaultValue,
                DefaultName = DefaultSystemNamed == false ? DefaultName : null,
                GenerationType = ComputedDefinition != null ? "COMPUTED"
                    : IsIdentity ? $"IDENTITY({IdentitySeed ?? "1"},{IdentityIncrement ?? "1"})" : null,
                ComputedDefinition = ComputedDefinition,
                IsPersisted = IsPersisted
            };
        }

        private sealed class IndexRow
        {
            public string TableName { get; set; } = string.Empty;
            public string IndexName { get; set; } = string.Empty;
            public int IndexId { get; set; }
            public string TypeDesc { get; set; } = string.Empty;
            public bool IsUnique { get; set; }
            public bool IsPrimaryKey { get; set; }
            public bool IsUniqueConstraint { get; set; }
            public bool ConstraintSystemNamed { get; set; }
            public bool HasFilter { get; set; }
            public string? FilterDefinition { get; set; }
            public string ColumnName { get; set; } = string.Empty;
            public int KeyOrdinal { get; set; }
            public bool IsDescending { get; set; }
            public bool IsIncluded { get; set; }
        }

        private sealed class ForeignKeyRow
        {
            public string TableName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsSystemNamed { get; set; }
            public string RefSchema { get; set; } = string.Empty;
            public string RefTable { get; set; } = string.Empty;
            public string ColumnName { get; set; } = string.Empty;
            public string RefColumnName { get; set; } = string.Empty;
            public string OnDelete { get; set; } = string.Empty;
            public string OnUpdate { get; set; } = string.Empty;
        }

        private sealed class CheckRow
        {
            public string TableName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsSystemNamed { get; set; }
            public string Definition { get; set; } = string.Empty;
        }

        private sealed class SequenceRow
        {
            public string Name { get; set; } = string.Empty;
            public string DataType { get; set; } = string.Empty;
            public string StartValue { get; set; } = string.Empty;
            public string Increment { get; set; } = string.Empty;
            public string MinValue { get; set; } = string.Empty;
            public string MaxValue { get; set; } = string.Empty;
            public bool IsCycling { get; set; }
        }
    }
}
