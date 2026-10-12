using APIGateWay.BusinessLayer.Auth;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.nugetmodal;
using APIGateWay.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace APIGateWay.Tests
{
    public class SyncRequestEnricherTests
    {
        private static readonly UserRepoAccess OwnRepo = new() { RepoId = Guid.NewGuid(), RepoKey = "OWN" };
        private static readonly Guid OwnTicket = Guid.NewGuid();
        private static readonly Guid OtherTicket = Guid.NewGuid();

        private sealed class FakeRepoAccess : IRepoAccessService
        {
            public int TicketLookups;
            public Task<List<string>> GetUserRepoIdsAsync(Guid userId) => Task.FromResult(new List<string> { OwnRepo.RepoKey });
            public Task<List<UserRepoAccess>> GetUserRepoGuidsAsync(Guid userId) => Task.FromResult(new List<UserRepoAccess> { OwnRepo });
            public void Forget(IEnumerable<Guid> userIds) { }
            public Task<string?> GetTicketRepoKeyAsync(Guid ticketId)
            {
                TicketLookups++;
                return Task.FromResult<string?>(ticketId == OwnTicket ? "OWN" : ticketId == OtherTicket ? "OTHER" : null);
            }
        }

        private static (SyncRequestEnricher enricher, FakeRepoAccess repoAccess) Make(int role)
        {
            var http = new DefaultHttpContext();
            http.Items["AllowedRepos"] = new List<UserRepoAccess> { OwnRepo };
            var repoAccess = new FakeRepoAccess();
            var enricher = new SyncRequestEnricher(
                new FakeLoginContextService { role = role },
                new HttpContextAccessor { HttpContext = http },
                repoAccess);
            return (enricher, repoAccess);
        }

        private static DynamicSyncRequest Request(string key, Dictionary<string, string>? p = null)
        {
            var request = new DynamicSyncRequest { ConfigKeys = { key } };
            if (p != null) request.Params[key] = p;
            return request;
        }

        // ── Deny by default ─────────────────────────────────────────────────

        [Theory]
        [InlineData("MeetingData")]
        [InlineData("UpcomingMeeting")]
        [InlineData("GetUserOnlineStatus")]
        [InlineData("TeamMaster")]
        [InlineData("TimeSheet")]
        [InlineData("TimesheetV2")]
        [InlineData("SomeNewKey")]
        public async Task Client_UnlistedKey_IsDenied(string key)
        {
            var (enricher, _) = Make(AppRoles.Viewer);

            var result = await enricher.EnrichAsync(Request(key));

            Assert.Empty(result.Units);
            Assert.True(result.DeniedKeys.ContainsKey(key));
        }

        [Theory]
        [InlineData(AppRoles.Admin)]
        [InlineData(AppRoles.Manager)]
        public async Task Staff_UnlistedKey_PassesThrough(int role)
        {
            var (enricher, _) = Make(role);

            var result = await enricher.EnrichAsync(Request("MeetingData"));

            Assert.Single(result.Units);
            Assert.Empty(result.DeniedKeys);
        }

        [Fact]
        public async Task Client_GlobalLookup_PassesThrough()
        {
            var (enricher, _) = Make(AppRoles.Viewer);

            var result = await enricher.EnrichAsync(Request("LabelMaster"));

            Assert.Single(result.Units);
            Assert.Empty(result.DeniedKeys);
        }

        // ── Ticket-scoped keys ─────────────────────────────────────────────

        [Fact]
        public async Task Client_TicketKey_OwnTicket_Runs()
        {
            var (enricher, _) = Make(AppRoles.Viewer);

            var result = await enricher.EnrichAsync(Request("TicketHistory", new() { ["IssuesId"] = OwnTicket.ToString() }));

            Assert.Single(result.Units);
            Assert.Empty(result.DeniedKeys);
        }

        [Fact]
        public async Task Client_TicketKey_OtherReposTicket_IsDenied()
        {
            var (enricher, _) = Make(AppRoles.Viewer);

            var result = await enricher.EnrichAsync(Request("TicketHistory", new() { ["IssuesId"] = OtherTicket.ToString() }));

            Assert.Empty(result.Units);
            Assert.True(result.DeniedKeys.ContainsKey("TicketHistory"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-guid")]
        public async Task Client_TicketKey_WithoutTicketId_IsDenied(string? ticketId)
        {
            var (enricher, _) = Make(AppRoles.Viewer);
            var p = ticketId == null ? null : new Dictionary<string, string> { ["IssuesId"] = ticketId };

            var result = await enricher.EnrichAsync(Request("TicketHistory", p));

            Assert.Empty(result.Units);
            Assert.True(result.DeniedKeys.ContainsKey("TicketHistory"));
        }

        [Fact]
        public async Task Client_SameTicketAcrossKeys_LooksUpOnce()
        {
            var (enricher, repoAccess) = Make(AppRoles.Viewer);
            var p = new Dictionary<string, string> { ["IssuesId"] = OwnTicket.ToString() };
            var request = new DynamicSyncRequest { ConfigKeys = { "ThreadsList", "TicketHistory", "TicketIssueLog" } };
            foreach (var key in request.ConfigKeys) request.Params[key] = new(p);

            var result = await enricher.EnrichAsync(request);

            Assert.Equal(3, result.Units.Count);
            Assert.Equal(1, repoAccess.TicketLookups);
            Assert.Equal(AppRoles.Viewer.ToString(), result.Units.Single(u => u.ConfigKey == "ThreadsList").Params["Role"]);
        }

        // ── Repo-scoped keys ───────────────────────────────────────────────

        [Fact]
        public async Task Client_RepoKey_NoRepoSent_FansOutToOwnRepos()
        {
            var (enricher, _) = Make(AppRoles.Viewer);

            var result = await enricher.EnrichAsync(Request("ClientData"));

            var unit = Assert.Single(result.Units);
            Assert.Equal(OwnRepo.RepoId.ToString(), unit.Params["Repo_Id"]);
        }

        [Fact]
        public async Task Client_RepoKey_OtherRepoSent_IsDenied()
        {
            var (enricher, _) = Make(AppRoles.Viewer);

            var result = await enricher.EnrichAsync(Request("ClientData", new() { ["Repo_Id"] = Guid.NewGuid().ToString() }));

            Assert.Empty(result.Units);
            Assert.True(result.DeniedKeys.ContainsKey("ClientData"));
        }
    }
}
