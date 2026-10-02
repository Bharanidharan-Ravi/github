using System.Text;
using System.Text.RegularExpressions;
using APIGateWay.ModalLayer.DTOs;
using static APIGateWay.Business_Layer.DbMigration.SqlSchemaReader;

namespace APIGateWay.Business_Layer.DbMigration
{
    /// <summary>One applied (or planned) DDL statement and what undoes it. Persisted in the rollback file.</summary>
    public class MigrationStep
    {
        public const string CREATE_TABLE = "CREATE_TABLE";
        public const string ADD_COLUMN = "ADD_COLUMN";
        public const string ALTER_COLUMN = "ALTER_COLUMN";
        public const string CREATE_PROCEDURE = "CREATE_PROCEDURE";
        public const string UPDATE_PROCEDURE = "UPDATE_PROCEDURE";

        public string Kind { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;   // table or procedure name
        public string? Column { get; set; }                  // ADD_COLUMN / ALTER_COLUMN
        public string Sql { get; set; } = string.Empty;
        public List<string>? PostSql { get; set; }           // CREATE_TABLE: indexes, checks, foreign keys — run after every new table exists
        public string? Settings { get; set; }                // procedures: SET ANSI_NULLS / QUOTED_IDENTIFIER to create it with (null = both ON)
        public string? PreviousDdl { get; set; }             // UPDATE_PROCEDURE / ALTER_COLUMN: statement restoring the live definition
        public string? PreviousSettings { get; set; }        // UPDATE_PROCEDURE: SET options of the live definition
        public string? Change { get; set; }                  // ALTER_COLUMN: "old type -> new type", shown in the report
        public bool NotNullWithoutDefault { get; set; }      // ADD_COLUMN: only possible on an empty table
        public bool BecomesNotNull { get; set; }             // ALTER_COLUMN: refused while the column holds NULLs
        public string? ShrinkFilter { get; set; }            // ALTER_COLUMN: rows matching it would be cut, so it goes to manual if any exist
        public bool ConvertsType { get; set; }               // ALTER_COLUMN: not a plain widening — only allowed while the column holds no values

        public string Label => Column == null ? Object : $"{Object}.{Column}";
    }

    public class MigrationPlan
    {
        public List<MigrationStep> Steps { get; } = new();
        public List<DbMigrationNoteDto> Manual { get; } = new();
        public List<string> OnlyInTarget { get; } = new();
    }

    /// <summary>
    /// Compares a source structure snapshot with the target DB. Only additive, data-safe changes
    /// become steps: new tables, new columns, column type changes, new/changed procedures. Everything else is a note.
    /// </summary>
    internal static class SchemaDiffer
    {
        // Leading comments are kept in sys.sql_modules, so CREATE may come after them.
        private static readonly Regex CreateProcedureHead = new(
            @"^(?<lead>(?:\s+|--[^\r\n]*(?:\r?\n|$)|/\*[\s\S]*?\*/)*)CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static MigrationPlan BuildPlan(SchemaSnapshot source, SchemaSnapshot target)
        {
            var plan = new MigrationPlan();
            var live = target.Objects.Where(o => !IsTrackingObject(o.Name))
                .ToDictionary(o => (o.Type, o.Name.ToUpperInvariant()));
            var wanted = source.Objects.Where(o => !IsTrackingObject(o.Name)).ToList();
            string Rewrite(string ddl) =>
                RewriteName(RewriteName(ddl, source.Database, target.Database), source.Schema, target.Schema);

            foreach (var src in wanted.Where(o => o.Type == TABLE))
            {
                if (!live.TryGetValue((TABLE, src.Name.ToUpperInvariant()), out var dst))
                {
                    if (src.Ddl == null) plan.Manual.Add(Note(src, "DDL was not captured in the snapshot"));
                    else
                    {
                        // CREATE TABLE first, then its GO-separated index/constraint statements.
                        var statements = SplitBatches(Rewrite(src.Ddl)).ToList();
                        plan.Steps.Add(new MigrationStep
                        {
                            Kind = MigrationStep.CREATE_TABLE,
                            Object = src.Name,
                            Sql = statements[0],
                            PostSql = statements.Skip(1).ToList()
                        });
                    }
                    continue;
                }

                var liveCols = (dst.Columns ?? new()).ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var col in src.Columns ?? new())
                {
                    if (!liveCols.TryGetValue(col.Name, out var liveCol))
                    {
                        if (col.GenerationType != null)
                            plan.Manual.Add(new() { Object = $"{src.Name}.{col.Name}", Reason = $"generated/identity column ({col.GenerationType})" });
                        else
                            plan.Steps.Add(new MigrationStep
                            {
                                Kind = MigrationStep.ADD_COLUMN,
                                Object = dst.Name,
                                Column = col.Name,
                                Sql = $"ALTER TABLE {Q(target.Schema)}.{Q(dst.Name)} ADD {ColumnDefinition(col)}",
                                NotNullWithoutDefault = !col.IsNullable && col.DefaultValue == null
                            });
                    }
                    else if (!string.Equals(TypeSpec(col), TypeSpec(liveCol), StringComparison.OrdinalIgnoreCase) || col.IsNullable != liveCol.IsNullable)
                    {
                        if (col.GenerationType != null || liveCol.GenerationType != null)
                            plan.Manual.Add(new() { Object = $"{src.Name}.{col.Name}", Reason = "generated/identity column — alter manually" });
                        else
                        {
                            var lengthOnly = IsLengthChangeOnly(liveCol, col);
                            plan.Steps.Add(new MigrationStep
                            {
                                Kind = MigrationStep.ALTER_COLUMN,
                                Object = dst.Name,
                                Column = liveCol.Name,
                                Sql = AlterColumnSql(target.Schema, dst.Name, liveCol.Name, col),
                                PreviousDdl = AlterColumnSql(target.Schema, dst.Name, liveCol.Name, liveCol),
                                Change = $"{Describe(liveCol)} -> {Describe(col)}",
                                BecomesNotNull = liveCol.IsNullable && !col.IsNullable,
                                ShrinkFilter = lengthOnly && Len(col) < Len(liveCol)
                                    ? $"{(IsBinary(col) ? "DATALENGTH" : "LEN")}({Q(liveCol.Name)}) > {col.Length}"
                                    : null,
                                ConvertsType = !lengthOnly && !IsWidening(liveCol, col)
                            });
                        }
                    }
                }
                var srcCols = (src.Columns ?? new()).Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                plan.OnlyInTarget.AddRange(liveCols.Keys.Where(c => !srcCols.Contains(c)).Select(c => $"COLUMN {dst.Name}.{c}"));
            }

            foreach (var src in wanted.Where(o => o.Type == PROCEDURE))
            {
                if (src.Ddl == null) { plan.Manual.Add(Note(src, "DDL was not captured in the snapshot (encrypted?)")); continue; }
                var sql = ToCreateOrAlter(Rewrite(src.Ddl));
                var settings = SettingsFor(src);

                if (!live.TryGetValue((PROCEDURE, src.Name.ToUpperInvariant()), out var dst))
                    plan.Steps.Add(new MigrationStep { Kind = MigrationStep.CREATE_PROCEDURE, Object = src.Name, Sql = sql, Settings = settings });
                else if (dst.Ddl == null)
                    plan.Manual.Add(Note(src, "live definition could not be read, so it cannot be compared or rolled back"));
                else if (Normalize(sql) != Normalize(ToCreateOrAlter(dst.Ddl)) || settings != SettingsFor(dst))
                    plan.Steps.Add(new MigrationStep
                    {
                        Kind = MigrationStep.UPDATE_PROCEDURE,
                        Object = dst.Name,
                        Sql = sql,
                        Settings = settings,
                        PreviousDdl = ToCreateOrAlter(dst.Ddl),
                        PreviousSettings = SettingsFor(dst)
                    });
            }

            // Views, functions, sequences, triggers: reported only.
            foreach (var src in wanted.Where(o => o.Type != TABLE && o.Type != PROCEDURE))
            {
                if (!live.TryGetValue((src.Type, src.Name.ToUpperInvariant()), out var dst))
                    plan.Manual.Add(Note(src, "new — only tables and procedures are applied automatically"));
                else if (src.Ddl != null && dst.Ddl != null && Normalize(Rewrite(src.Ddl)) != Normalize(dst.Ddl))
                    plan.Manual.Add(Note(src, "changed — only tables and procedures are applied automatically"));
            }

            var srcKeys = wanted.Select(o => (o.Type, o.Name.ToUpperInvariant())).ToHashSet();
            plan.OnlyInTarget.AddRange(live.Where(k => !srcKeys.Contains(k.Key)).Select(k => $"{k.Value.Type} {k.Value.Name}"));
            return plan;
        }

        /// <summary>Points DDL written against the source database/schema name at the target one ([FROM]. and FROM. prefixes).</summary>
        public static string RewriteName(string ddl, string from, string to)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return ddl;
            ddl = Regex.Replace(ddl, $@"\[{Regex.Escape(from)}\]\s*\.", $"[{to}].", RegexOptions.IgnoreCase);
            return Regex.Replace(ddl, $@"(?<![A-Za-z0-9_\[\]@#$\.]){Regex.Escape(from)}\s*\.", to + ".", RegexOptions.IgnoreCase);
        }

        public static string ToCreateOrAlter(string ddl) =>
            CreateProcedureHead.Replace(ddl, "${lead}CREATE OR ALTER PROCEDURE", 1);

        /// <summary>SET options to run before the CREATE, or null when they are the defaults (both ON).</summary>
        private static string? SettingsFor(SchemaObject o) =>
            o.AnsiNulls == false || o.QuotedIdentifier == false
                ? $"SET ANSI_NULLS {OnOff(o.AnsiNulls)}; SET QUOTED_IDENTIFIER {OnOff(o.QuotedIdentifier)};"
                : null;

        private static bool IsTrackingObject(string name) =>
            name.Contains(DbMigrationConstants.TRACKING_TABLE, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Definition reduced to what SQL Server runs: comments dropped, whitespace only kept between
        /// two words, keywords/identifiers upper-cased. String literals stay exactly as written.
        /// </summary>
        private static string Normalize(string ddl)
        {
            var sb = new StringBuilder(ddl.Length);
            var pendingSpace = false;
            for (var i = 0; i < ddl.Length; i++)
            {
                var c = ddl[i];
                if (c == '-' && i + 1 < ddl.Length && ddl[i + 1] == '-')
                {
                    while (i < ddl.Length && ddl[i] != '\n') i++;
                    pendingSpace = true;
                }
                else if (c == '/' && i + 1 < ddl.Length && ddl[i + 1] == '*')
                {
                    // Block comments nest in T-SQL.
                    var depth = 0;
                    for (; i < ddl.Length; i++)
                    {
                        if (ddl[i] == '/' && i + 1 < ddl.Length && ddl[i + 1] == '*') { depth++; i++; }
                        else if (ddl[i] == '*' && i + 1 < ddl.Length && ddl[i + 1] == '/') { i++; if (--depth == 0) break; }
                    }
                    pendingSpace = true;
                }
                else if (char.IsWhiteSpace(c)) pendingSpace = true;
                else
                {
                    if (pendingSpace && sb.Length > 0 && IsWordChar(sb[^1]) && IsWordChar(c)) sb.Append(' ');
                    pendingSpace = false;
                    if (c == '\'')
                    {
                        // Literal, '' is an escaped quote.
                        var end = i + 1;
                        while (end < ddl.Length && !(ddl[end] == '\'' && (end + 1 >= ddl.Length || ddl[end + 1] != '\''))) end += ddl[end] == '\'' ? 2 : 1;
                        sb.Append(ddl, i, Math.Min(end + 1, ddl.Length) - i);
                        i = end;
                    }
                    else sb.Append(char.ToUpperInvariant(c));
                }
            }
            return sb.ToString().TrimEnd(';');
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '@' or '#' or '$';

        private static DbMigrationNoteDto Note(SchemaObject o, string reason) => new() { Object = $"{o.Type} {o.Name}", Reason = reason };

        private static int Len(ColumnSchema c) => c.Length == -1 ? int.MaxValue : c.Length ?? 0;

        private static bool IsBinary(ColumnSchema c) => c.DataType.ToLowerInvariant() is "varbinary" or "binary";

        // 1 = varchar/char, 2 = nvarchar/nchar, 3 = varbinary/binary
        private static int Family(string type) => type switch
        {
            "varchar" or "char" => 1,
            "nvarchar" or "nchar" => 2,
            "varbinary" or "binary" => 3,
            _ => 0
        };

        /// <summary>
        /// Only the length changes (or varchar -> nvarchar): values stay as they are, as long as they fit.
        /// A fixed-width target is only accepted from a fixed-width source (no padding added).
        /// </summary>
        private static bool IsLengthChangeOnly(ColumnSchema from, ColumnSchema to)
        {
            string f = from.DataType.ToLowerInvariant(), t = to.DataType.ToLowerInvariant();
            int ff = Family(f), tf = Family(t);
            if (ff == 0 || tf == 0 || !(ff == tf || (ff == 1 && tf == 2))) return false;
            var targetFixed = t is "char" or "nchar" or "binary";
            var sourceFixed = f is "char" or "nchar" or "binary";
            return !targetFixed || sourceFixed;
        }

        // Integer types in widening order.
        private static readonly string[] IntegerTypes = { "tinyint", "smallint", "int", "bigint" };

        /// <summary>True when every existing value fits the new type unchanged (same type or a wider one).</summary>
        private static bool IsWidening(ColumnSchema from, ColumnSchema to)
        {
            string f = from.DataType.ToLowerInvariant(), t = to.DataType.ToLowerInvariant();
            if (string.Equals(TypeSpec(from), TypeSpec(to), StringComparison.OrdinalIgnoreCase)) return true; // nullability only
            int fi = Array.IndexOf(IntegerTypes, f), ti = Array.IndexOf(IntegerTypes, t);
            if (fi >= 0 && ti >= 0) return ti >= fi;
            if (f is "decimal" or "numeric" && t is "decimal" or "numeric")
                return (to.Scale ?? 0) >= (from.Scale ?? 0) && (to.Precision - to.Scale) >= (from.Precision - from.Scale);
            if (f == "datetime2" && t == "datetime2") return (to.Scale ?? 7) >= (from.Scale ?? 7);
            return false;
        }

        private static string Describe(ColumnSchema c) => TypeSpec(c) + (c.IsNullable ? " NULL" : " NOT NULL");

        private static string AlterColumnSql(string schema, string table, string column, ColumnSchema c) =>
            $"ALTER TABLE {Q(schema)}.{Q(table)} ALTER COLUMN {Q(column)} {TypeSpec(c)}{(c.IsNullable ? " NULL" : " NOT NULL")}";
    }
}
