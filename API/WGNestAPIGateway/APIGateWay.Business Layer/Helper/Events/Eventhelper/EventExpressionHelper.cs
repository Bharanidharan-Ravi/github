using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Helper.Events.Eventhelper
{
    public static class EventExpressionHelper
    {
        public static object CreatePredicate(
     Type entityType,
     string propertyName,
     string entityId)
        {
            var parameter =
                Expression.Parameter(
                    entityType,
                    "p");

            var property =
                Expression.Property(
                    parameter,
                    propertyName);

            object convertedValue;

            var propertyType =
                Nullable.GetUnderlyingType(
                    property.Type) ?? property.Type;

            if (propertyType == typeof(Guid))
            {
                convertedValue = Guid.Parse(entityId);
            }
            else if (propertyType == typeof(int))
            {
                convertedValue = int.Parse(entityId);
            }
            else if (propertyType == typeof(long))
            {
                convertedValue = long.Parse(entityId);
            }
            else
            {
                convertedValue = entityId;
            }

            var constant =
                Expression.Constant(
                    convertedValue,
                    property.Type);

            var body =
                Expression.Equal(
                    property,
                    constant);

            var lambdaType =
                typeof(Func<,>)
                    .MakeGenericType(
                        entityType,
                        typeof(bool));

            return Expression.Lambda(
                lambdaType,
                body,
                parameter)
                .Compile();
        }
    }
}
