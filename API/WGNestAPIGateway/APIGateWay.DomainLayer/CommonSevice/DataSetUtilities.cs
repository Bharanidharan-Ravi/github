using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public static class DataSetUtilities
    {
        public static T AutoCastFieldHelper<T>(DataRow dataRow, string fieldName)
        {
            try
            {
                if (!dataRow.Table.Columns.Contains(fieldName))
                {
                    return default(T);
                }

                if (dataRow.IsNull(fieldName))
                {
                    return default(T);
                }

                return dataRow.Field<T>(fieldName);
            }
            catch (Exception)
            {
                throw;
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
