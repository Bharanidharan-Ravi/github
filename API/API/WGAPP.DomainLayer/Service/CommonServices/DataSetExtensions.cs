using WGAPP.DomainLayer.Services.CommonServices.DataSetUtilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WGAPP.DomainLayer.Service.CommonService
{
    public static class  DataSetExtensions
    {
        //public static T AutoCast<T>(this DataRow dataRow) where T : class
        //{
        //    try
        //    {
        //        var type = typeof(T);
        //        return (T)AutoCast(dataRow, type);
        //    }
        //    catch (Exception) { throw; }
        //}

        public static T AutoCast<T>(this DataRow row) where T : new()
        {
            var obj = new T();
            var properties = typeof(T).GetProperties();

            foreach (var prop in properties)
            {
                string colName = prop.Name;

                if (!row.Table.Columns.Contains(colName))
                    continue;

                try
                {
                    object value = row[colName];

                    if (value == DBNull.Value)
                        continue;

                    // If target property is string or primitive
                    if (prop.PropertyType == typeof(string) ||
                        prop.PropertyType.IsPrimitive ||
                        prop.PropertyType.IsValueType)
                    {
                        prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                    }
                    else if (value is string jsonString)
                    {
                        // Target property is object → treat string as JSON
                        var deserialized = JsonConvert.DeserializeObject(jsonString, prop.PropertyType);
                        prop.SetValue(obj, deserialized);
                    }
                    else
                    {
                        // fallback
                        prop.SetValue(obj, value);
                    }
                }
                catch { /* ignore and continue */ }
            }

            return obj;
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
    }
}
