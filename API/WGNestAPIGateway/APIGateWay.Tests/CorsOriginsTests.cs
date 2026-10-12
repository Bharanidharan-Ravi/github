using APIGateWay.BusinessLayer.Helpers;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace APIGateWay.Tests
{
    public class CorsOriginsTests
    {
        private static IConfiguration Config(params string?[] origins)
        {
            var values = origins
                .Select((o, i) => new KeyValuePair<string, string?>($"{CorsOrigins.SectionName}:{i}", o));
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        // Same policy shape as Program.cs "FrontendPolicy"
        private static CorsResult Preflight(IConfiguration config, string origin)
        {
            var policy = new CorsPolicyBuilder()
                .SetIsOriginAllowed(o => CorsOrigins.IsAllowed(config, o))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .Build();

            var context = new DefaultHttpContext();
            context.Request.Method = "OPTIONS";
            context.Request.Headers.Origin = origin;
            context.Request.Headers.AccessControlRequestMethod = "POST";

            var service = new CorsService(Options.Create(new CorsOptions()), NullLoggerFactory.Instance);
            return service.EvaluatePolicy(context, policy);
        }

        [Theory]
        [InlineData("https://nest.workglow.in")]
        [InlineData("http://localhost:5173")]
        public void Allows_listed_front_ends(string origin)
        {
            var result = Preflight(Config("https://nest.workglow.in/", "http://localhost:5173"), origin);

            Assert.True(result.IsOriginAllowed);
            Assert.True(result.SupportsCredentials);
        }

        [Theory]
        [InlineData("https://attacker.example")]
        [InlineData("https://nest.workglow.in.attacker.example")]
        [InlineData("http://nest.workglow.in")]
        [InlineData("null")]
        public void Rejects_other_origins(string origin)
        {
            var result = Preflight(Config("https://nest.workglow.in", "http://localhost:5173"), origin);

            Assert.False(result.IsOriginAllowed);
        }

        [Fact]
        public void Wildcard_and_blank_entries_are_ignored()
        {
            var origins = CorsOrigins.Load(Config("*", " ", "https://nest.workglow.in/"));

            Assert.Equal(new[] { "https://nest.workglow.in" }, origins);
            Assert.False(Preflight(Config("*"), "https://attacker.example").IsOriginAllowed);
        }

        [Fact]
        public void Origin_list_changes_apply_without_restart()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [$"{CorsOrigins.SectionName}:0"] = "https://nest.workglow.in" })
                .Build();

            Assert.False(Preflight(config, "http://localhost:5173").IsOriginAllowed);

            config[$"{CorsOrigins.SectionName}:1"] = "http://localhost:5173";

            Assert.True(Preflight(config, "http://localhost:5173").IsOriginAllowed);
        }

        [Fact]
        public void Missing_config_falls_back_to_live_front_end_only()
        {
            Assert.Equal(CorsOrigins.Default, CorsOrigins.Load(Config()));
        }
    }
}
