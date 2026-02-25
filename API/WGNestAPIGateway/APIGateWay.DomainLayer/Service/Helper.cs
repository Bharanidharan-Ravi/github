using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.Interface; // Make sure this matches where IAuditableEntity/User live
using APIGateWay.ModalLayer.Helper;
using APIGateWay.ModalLayer.MasterData; // Make sure this matches where your models live
using AutoMapper;
using Microsoft.Data.SqlClient;
using ReverseMarkdown;
using System;
using System.Linq;
using static APIGateWay.ModalLayer.Helper.HelperModal;
using static APIGateWay.ModalLayer.Helper.PostHelper;


namespace APIGateWay.DomainLayer.Helpers
{
    // =========================================================================
    // 2. HTML TO PLAIN TEXT UTILITY
    // =========================================================================
    public static class HtmlUtilities
    {
        public static string ConvertToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;

            var config = new Config
            {
                // Converts HTML tables to Markdown plain-text tables
                GithubFlavored = true,
                // Drops tags it doesn't recognize instead of crashing
                UnknownTags = Config.UnknownTagsOption.Drop,
                // Removes completely empty tags
                RemoveComments = true,
                SmartHrefHandling = true
            };

            var converter = new Converter(config);

            // This turns <ol><li>Test</li></ol> into "1. Test"
            // It turns <strong>Bold</strong> into "**Bold**"
            return converter.Convert(html).Trim();
        }
    }

    // =========================================================================
    // 3. AUTOMAPPER DYNAMIC EXTENSIONS
    // =========================================================================
    public static class AutoMapperExtensions
    {
        // This is the "Engine" that reads the [IgnoreMapping] sticky notes
        public static IMappingExpression<TSource, TDest> ApplyDynamicIgnores<TSource, TDest>(
            this IMappingExpression<TSource, TDest> expression)
        {
            var destType = typeof(TDest);
            var auditableEntityProps = typeof(IAuditableEntity).GetProperties().Select(p => p.Name).ToList();
            var auditableUserProps = typeof(IAuditableUser).GetProperties().Select(p => p.Name).ToList();

            expression.ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            foreach (var property in destType.GetProperties())
            {
                // Finds properties with [IgnoreMapping] and tells AutoMapper to skip them
                if (property.GetCustomAttributes(typeof(IgnoreMappingAttribute), true).Any())
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                    continue;
                }

                if (typeof(IAuditableEntity).IsAssignableFrom(destType) && auditableEntityProps.Contains(property.Name))
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                    continue;
                }

                if (typeof(IAuditableUser).IsAssignableFrom(destType) && auditableUserProps.Contains(property.Name))
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                    continue;
                }
            }
            return expression;
        }
    }

    public static class CommonServiceExtensions
    {
        public static async Task<SequenceResult> GetNextSequenceAsync(
            this APIGateWayCommonService commonService,
            string seriesName,
            string? columnName = null,
            string? currentSeriesName = null)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@SeriesName", (object)seriesName ?? DBNull.Value)
            };

            // Only add these parameters if they are provided, matching your SQL SP logic
            if (!string.IsNullOrEmpty(columnName))
            {
                parameters.Add(new SqlParameter("@ColumnName", columnName));
            }

            if (!string.IsNullOrEmpty(currentSeriesName))
            {
                parameters.Add(new SqlParameter("@CurrentSeriesName", currentSeriesName));
            }

            var nextSeq = await commonService.ExecuteGetItemAsyc<SequenceResult>(
                "GetNextNumber",
                parameters.ToArray()
            );

            return nextSeq[0];
        }
    }
}