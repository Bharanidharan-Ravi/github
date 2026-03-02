using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public static class DataSetExtensions
    {
        public static T AutoCast<T>(this DataRow dataRow) where T : class
        {
            try
            {
                var type = typeof(T);
                return (T)AutoCast(dataRow, type);
            }
            catch (Exception) { throw; }
        }

        public static List<T> MapTable<T>(this DataTable table) where T : class, new()
        {
            if (table == null || table.Rows.Count == 0)
                return new List<T>();

            return table.AsEnumerable()
                .Select(row => row.AutoCast<T>())
                .ToList();
        }
        private static object AutoCast(DataRow dataRow, Type type, string classPrefix = null)
        {
            try
            {
                var properties = type.GetProperties();
                var instance = type.GetConstructor(Type.EmptyTypes).Invoke(new object[] { });
                foreach (var prop in properties)
                {
                    // if it's a user defined type, it's in the HDS.Analyst namespace, and it's not an enum, try to autocast it
                    // the fields are expected to be prefixed with the property's name plus two underscores, e.g. Modifier__ObjectID in UserNotificationSetting
                    if (!prop.PropertyType.IsPrimitive && prop.PropertyType.Namespace.StartsWith("HDS.Analyst") && !prop.PropertyType.IsEnum)
                    {
                        SetPropertyValue(instance, prop, AutoCast(dataRow, prop.PropertyType, prop.Name));
                    }
                    else
                    {
                        // prop method:
                        var method = typeof(DataSetUtilities).GetMethod("AutoCastFieldHelper").MakeGenericMethod(new Type[] { prop.PropertyType });
                        string propName = classPrefix == null ? string.Empty : classPrefix + "__";
                        propName += prop.Name;
                        object propVal = method.Invoke(null, new object[] { dataRow, propName });
                        SetPropertyValue(instance, prop, propVal);
                    }
                }
                return instance;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void SetPropertyValue<T>(T instance, PropertyInfo prop, object propVal) where T : class
        {
            try
            {
                if (prop.SetMethod != null)
                {
                    prop.SetMethod.Invoke(instance, new object[] { propVal });
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static List<T> DeserializeToList<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<T>();

            var token = JToken.Parse(json);

            return token.Type switch
            {
                JTokenType.Array => token.ToObject<List<T>>() ?? new List<T>(),
                JTokenType.Object => new List<T> { token.ToObject<T>()! },
                _ => new List<T>()
            };
        }
    }
}
