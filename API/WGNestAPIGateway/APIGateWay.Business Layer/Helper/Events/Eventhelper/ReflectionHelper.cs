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
}
