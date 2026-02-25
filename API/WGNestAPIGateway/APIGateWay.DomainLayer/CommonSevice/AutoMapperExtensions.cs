using APIGateWay.ModalLayer.Helper;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.PostHelper;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public static class AutoMapperExtensions
    {
        public static IMappingExpression<TSource, TDest> ApplyDynamicIgnores<TSource, TDest>(
            this IMappingExpression<TSource, TDest> expression)
        {
            var destType = typeof(TDest);
            var auditableEntityProps = typeof(IAuditableEntity).GetProperties().Select(p => p.Name).ToList();
            var auditableUserProps = typeof(IAuditableUser).GetProperties().Select(p => p.Name).ToList();

            // Solves the Null Exception
            expression.ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            foreach (var property in destType.GetProperties())
            {
                // 1. Ignore fields marked with [IgnoreMapping]
                if (property.GetCustomAttributes(typeof(IgnoreMappingAttribute), true).Any())
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                    continue;
                }

                // 2. Ignore IAuditableEntity fields
                if (typeof(IAuditableEntity).IsAssignableFrom(destType) && auditableEntityProps.Contains(property.Name))
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                    continue;
                }

                // 3. Ignore IAuditableUser fields
                if (typeof(IAuditableUser).IsAssignableFrom(destType) && auditableUserProps.Contains(property.Name))
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                    continue;
                }
            }
            return expression;
        }
    }
}