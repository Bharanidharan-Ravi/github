using Microsoft.Extensions.Configuration;

namespace APIGateWay.BusinessLayer.Helpers
{
    /// <summary>
    /// Front-end origins allowed to call the API with credentials (config "Cors:AllowedOrigins").
    /// Never allow every origin: together with AllowCredentials that lets any website act as the user.
    /// </summary>
    public static class CorsOrigins
    {
        public const string SectionName = "Cors:AllowedOrigins";
        public static readonly string[] Default = { "https://nest.workglow.in" };

        public static string[] Load(IConfiguration configuration)
        {
            var origins = (configuration.GetSection(SectionName).Get<string[]>() ?? Array.Empty<string>())
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim().TrimEnd('/'))
                .Where(o => o != "*") // a wildcard would re-open the hole
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return origins.Length > 0 ? origins : Default;
        }

        /// <summary>
        /// Reads the list on every call so edits to appsettings.json (reloadOnChange) apply without an app-pool restart.
        /// </summary>
        public static bool IsAllowed(IConfiguration configuration, string origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
                return false;

            return Load(configuration).Contains(origin.Trim().TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
        }
    }
}
