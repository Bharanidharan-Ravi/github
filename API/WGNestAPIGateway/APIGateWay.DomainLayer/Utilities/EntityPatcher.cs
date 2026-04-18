using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Utilities
{
    /// <summary>
    /// Fluent builder that tracks field-level changes and applies patches
    /// only when old != new. Reusable across all repos.
    /// </summary>
    public class EntityPatcher<TEntity> where TEntity : class
    {
        private readonly TEntity _entity;
        private readonly List<Action> _pendingPatches = new();
        private readonly List<FieldChange> _detectedChanges = new();

        public EntityPatcher(TEntity entity) => _entity = entity;

        /// <summary>
        /// Register a field. Patch is queued only if old != new.
        /// </summary>
        public EntityPatcher<TEntity> Set<TValue>(
            string fieldName,
            TValue? currentValue,
            TValue? incomingValue,
            Action<TEntity, TValue?> applyPatch,
            Func<TValue?, string?>? display = null)  // optional custom display formatter
        {
            if (!ChangeTracker.IsChanged(currentValue, incomingValue))
                return this;

            _pendingPatches.Add(() => applyPatch(_entity, incomingValue));

            _detectedChanges.Add(new FieldChange(
                fieldName,
                display != null ? display(currentValue) : currentValue?.ToString(),
                display != null ? display(incomingValue) : incomingValue?.ToString()
            ));

            return this;
        }

        /// <summary>All field changes detected so far.</summary>
        public IReadOnlyList<FieldChange> Changes => _detectedChanges;

        /// <summary>True if at least one field changed.</summary>
        public bool HasChanges => _detectedChanges.Count > 0;

        /// <summary>Apply all queued patches to the entity.</summary>
        public TEntity Apply()
        {
            foreach (var patch in _pendingPatches) patch();
            return _entity;
        }
    }
}