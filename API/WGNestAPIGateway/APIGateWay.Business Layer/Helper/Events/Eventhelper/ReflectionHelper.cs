using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper.Events.Eventhelper
{
    public static class ReflectionHelper
    {
        public static T? GetPropertyValue<T>(
            object source,
            string propertyName)
        {
            if (source == null)
                return default;

            var property =
                source.GetType()
                      .GetProperty(propertyName);

            if (property == null)
                return default;

            return (T?)property.GetValue(source);
        }
    }
    public static class FlagHistoryHelper
    {
        public static string GetFlagChangeSummary(
            List<HistoryLabelDto> oldFlags,
            List<HistoryLabelDto> newFlags,
            string fieldName)
        {
            oldFlags ??= new List<HistoryLabelDto>();
            newFlags ??= new List<HistoryLabelDto>();

            var addedFlags = newFlags
                .Where(n => !oldFlags.Any(o => o.id == n.id))
                .ToList();

            var removedFlags = oldFlags
                .Where(o => !newFlags.Any(n => n.id == o.id))
                .ToList();


            var addedNames = addedFlags.Any()
                ? string.Join(", ", addedFlags.Select(x => x.name))
                : string.Empty;

            var removedNames = removedFlags.Any()
                ? string.Join(", ", removedFlags.Select(x => x.name))
                : string.Empty;


            if (!oldFlags.Any() && newFlags.Any())
            {
                return $"{fieldName} added 'None' to {addedNames}";
            }
            else if (oldFlags.Any() && !newFlags.Any())
            {
                return $"{fieldName} removed: {removedNames}";
            }
            else if (addedFlags.Any() && !removedFlags.Any())
            {
                return $"{fieldName} added: {addedNames}";
            }
            else if (removedFlags.Any() && !addedFlags.Any())
            {
                return $"{fieldName} removed: {removedNames}";
            }
            else if (addedFlags.Any() && removedFlags.Any())
            {
                return $"{fieldName} updated. Added: {addedNames}, Removed: {removedNames}";
            }

            return $"{fieldName} updated";
        }
    }
}
