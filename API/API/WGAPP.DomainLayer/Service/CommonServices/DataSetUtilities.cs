using Newtonsoft.Json;
using System;
using System.Data;

namespace WGAPP.DomainLayer.Services.CommonServices.DataSetUtilities
{
    public static class DataSetUtilities
    {
        public static T AutoCastFieldHelper<T>(DataRow dataRow, string fieldName)
        {
            try
            {
                if (!dataRow.Table.Columns.Contains(fieldName) || dataRow.IsNull(fieldName))
                    return default;

                object value = dataRow[fieldName];

                // Case 1: property is primitive type or string → return directly
                if (typeof(T) == typeof(string) || typeof(T).IsPrimitive || typeof(T).IsValueType)
                {
                    return dataRow.Field<T>(fieldName);
                }

                // Case 2: value is string but target is object → treat as JSON
                if (value is string jsonString)
                {
                    if (string.IsNullOrWhiteSpace(jsonString))
                        return default;

                    return JsonConvert.DeserializeObject<T>(jsonString);
                }

                // Default fallback
                return dataRow.Field<T>(fieldName);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error casting field '{fieldName}' to '{typeof(T).Name}': {ex.Message}", ex);
            }
        }

        public static void DeleteEmptyRows(DataTable table, string[] columns)
        {
            try
            {
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    bool allEmpty = true;

                    for (int j = 0; j < columns.Length; j++)
                    {
                        if (!table.Rows[i].IsNull(columns[j]))
                        {
                            allEmpty = false;
                            break;
                        }
                    }

                    if (allEmpty)
                    {
                        table.Rows.RemoveAt(i--);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
