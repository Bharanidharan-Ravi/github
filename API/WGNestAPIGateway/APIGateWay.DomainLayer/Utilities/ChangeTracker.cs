using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Utilities
{
    // ── Represents a single detected field change ─────────────────────────────
    public record FieldChange(string FieldName, string? OldValue, string? NewValue);

    // ── Core static comparison helpers ───────────────────────────────────────
    public static class ChangeTracker
    {
        /// <summary>
        /// Compares two values. Strings are trimmed + case-insensitive.
        /// Nulls are handled safely.
        /// </summary>
        public static bool IsChanged<T>(T? oldValue, T? newValue)
        {
            if (oldValue is null && newValue is null) return false;
            if (oldValue is null || newValue is null) return true;

            // String: trim + OrdinalIgnoreCase
            if (oldValue is string oldStr && newValue is string newStr)
                return !string.Equals(oldStr.Trim(), newStr.Trim(), StringComparison.OrdinalIgnoreCase);

            // DateTime: date-only comparison (strip time if needed)
            if (oldValue is DateTime oldDt && newValue is DateTime newDt)
                return oldDt.Date != newDt.Date;

            return !EqualityComparer<T>.Default.Equals(oldValue, newValue);
        }

        /// <summary>
        /// Reflects over two objects of the same type and returns all changed properties.
        /// Useful for generic audit logging.
        /// </summary>
        public static List<FieldChange> DetectChanges<T>(T oldEntity, T newEntity) where T : class
        {
            var changes = new List<FieldChange>();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                var oldVal = prop.GetValue(oldEntity);
                var newVal = prop.GetValue(newEntity);

                bool changed = oldVal is string o && newVal is string n
                    ? !string.Equals(o.Trim(), n.Trim(), StringComparison.OrdinalIgnoreCase)
                    : !Equals(oldVal, newVal);

                if (changed)
                    changes.Add(new FieldChange(prop.Name, oldVal?.ToString(), newVal?.ToString()));
            }

            return changes;
        }
    }
}
