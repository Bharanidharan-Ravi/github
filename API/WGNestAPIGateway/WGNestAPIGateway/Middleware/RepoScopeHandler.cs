using APIGateway.Middleware;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;

public class RepoScopeHandler : AuthorizationHandler<RepoScopeRequirement>
{
    private readonly IRepoAccessService _repoAccessService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RepoScopeHandler(
        IRepoAccessService repoAccessService,
        IHttpContextAccessor httpContextAccessor)
    {
        _repoAccessService = repoAccessService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RepoScopeRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            context.Fail();
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (userIdClaim == null || roleClaim == null)
        {
            context.Fail();
            return;
        }

        var userId = Guid.Parse(userIdClaim);
        var role = int.Parse(roleClaim);

        // ================================
        // ROLE 1 → Full Access
        // ================================
        if (role == AppRoles.Admin || role == AppRoles.Manager)
        {
            context.Succeed(requirement);
            return;
        }

        var path = httpContext.Request.Path.Value?.ToLower();
        var method = httpContext.Request.Method.ToUpper();

        // ================================
        // ROLE 3 → Only Project + Ticket
        // ================================
        //if (role == AppRoles.Viewer)
        //{
        //    if (!path.Contains("project") && !path.Contains("ticket"))
        //    {
        //        context.Fail();
        //        return;
        //    }
        //}

        // ================================
        // Block Repo Creation for 2 & 3
        // ================================
        if ((role == AppRoles.Viewer) &&
            path.Contains("repo") &&
            method == "POST")
        {
            context.Fail();
            return;
        }

        // ================================
        // Fetch allowed repos
        // ================================
        var allowedRepos = await _repoAccessService.GetUserRepoGuidsAsync(userId);
        httpContext.Items["AllowedRepos"] = allowedRepos;
        var allowedRepoIds = allowedRepos.Select(x => x.RepoId).ToList();

        var requestedRepoId = await ExtractRepoId(httpContext);
        if (requestedRepoId == null)
        {
            context.Succeed(requirement);
            return;
        }

        if (!allowedRepoIds.Contains(requestedRepoId.Value))
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }

    private async Task<Guid?> ExtractRepoId(HttpContext context)
    {
        // 1️⃣ Route
        if (context.Request.RouteValues.TryGetValue("repoId", out var routeValue))
        {
            if (Guid.TryParse(routeValue?.ToString(), out var routeRepoId))
                return routeRepoId;
        }

        // 2️⃣ Query
        if (context.Request.Query.TryGetValue("repoId", out var queryValue))
        {
            if (Guid.TryParse(queryValue.FirstOrDefault(), out var queryRepoId))
                return queryRepoId;
        }

        // 3️⃣ Body (POST/PUT)
        if (context.Request.Method == "POST" ||
            context.Request.Method == "PUT")
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                var json = JsonDocument.Parse(body);

                if (json.RootElement.TryGetProperty("Repo_Id", out var repoProp))
                {
                    if (Guid.TryParse(repoProp.GetString(), out var bodyRepoId))
                        return bodyRepoId;
                }
            }
        }

        return null;
    }
}



















//using APIGateWay.BusinessLayer.Auth;
//using APIGateWay.DomainLayer.Interface;
//using APIGateWay.ModalLayer;
//using System.Text;
//using System.Text.Json;

//namespace APIGateway.Middleware
//{
//    public class RoleBasedAccessMiddleware
//    {
//        private readonly RequestDelegate _next;

//        private static readonly HashSet<string> _bypass =
//            new(StringComparer.OrdinalIgnoreCase)
//            {
//                "/realtime", "/swagger", "/uploads", "/uploadstemp",
//                "/api/login", "/favicon", "/health"
//            };

//        public RoleBasedAccessMiddleware(RequestDelegate next) => _next = next;

//        // IRepoAccessService injected per-request (scoped service)
//        public async Task InvokeAsync(HttpContext ctx, IRepoAccessService repoAccess)
//        {
//            var path = ctx.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
//            var method = ctx.Request.Method.ToUpperInvariant();

//            // ── 1. Always bypass certain paths ────────────────────────────────
//            if (_bypass.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
//            {
//                await _next(ctx);
//                return;
//            }

//            // ── 2. Read role decoded by HttpContextMiddleware ──────────────────
//            if (!int.TryParse(ctx.Items["UserDetail:Role"]?.ToString(), out var role) || role == 0)
//            {
//                // Unauthenticated — pass to existing auth pipeline
//                await _next(ctx);
//                return;
//            }

//            // ── 3. Role 1 → completely unrestricted, zero overhead ─────────────
//            if (role == AppRoles.Admin )
//            {
//                ctx.Items["AllowedRepos"] = null; // null = "unrestricted" signal
//                await _next(ctx);
//                return;
//            }

//            // ── 4. Role 2 / 3 → load their repos ONCE for this request ────────
//            if (!Guid.TryParse(ctx.Items["UserDetail:USERID"]?.ToString(), out var userId))
//            {
//                await Deny(ctx, 401, "UNAUTHORIZED", "Invalid user session.");
//                return;
//            }

//            List<UserRepoAccess> allowedRepos;
//            try
//            {
//                allowedRepos = await repoAccess.GetUserRepoGuidsAsync(userId);
//            }
//            catch (Exception ex)
//            {
//                Console.Error.WriteLine($"[RoleMiddleware] Repo load failed user={userId}: {ex.Message}");
//                await Deny(ctx, 500, "SERVER_ERROR", "Unable to validate repository access. Please try again.");
//                return;
//            }

//            // Cache in context — SyncRequestEnricher reads this (zero extra DB call)
//            ctx.Items["AllowedRepos"] = allowedRepos;
//            var allowedIds = allowedRepos.Select(r => r.RepoId).ToHashSet();

//            // ── 5. Route rule lookup ───────────────────────────────────────────
//            var rule = RouteAccessPolicy.Match(path, method);
//            if (rule == null)
//            {
//                await Deny(ctx, 403, "ACCESS_DENIED", "This resource is not accessible for your role.");
//                return;
//            }

//            if (!rule.AllowedRoles.Contains(role))
//            {
//                await Deny(ctx, 403, "ACCESS_DENIED", "You do not have permission to perform this action.");
//                return;
//            }

//            // ── 6. Repo scope validation ──────────────────────────────────────
//            if (rule.ValidateRepoScope)
//            {
//                var denial = await CheckRepoScopeAsync(ctx, method, allowedIds);
//                if (denial is not null)
//                {
//                    await Deny(ctx, 403, "REPO_ACCESS_DENIED", denial);
//                    return;
//                }
//            }

//            // ── 7. All checks passed — controller runs ─────────────────────────
//            await _next(ctx);
//        }

//        // ─────────────────────────────────────────────────────────────────────
//        // Repo scope helpers
//        // null return = OK   |   string return = denial reason
//        // ─────────────────────────────────────────────────────────────────────

//        private static async Task<string?> CheckRepoScopeAsync(
//            HttpContext ctx, string method, HashSet<Guid> allowed)
//        {
//            if (allowed.Count == 0)
//                return "You have not been assigned to any repository.";

//            return method switch
//            {
//                "GET" => CheckQuery(ctx.Request.Query, allowed),
//                "POST"
//                or "PUT" => await CheckBodyAsync(ctx, allowed),
//                _ => null
//            };
//        }

//        private static string? CheckQuery(IQueryCollection query, HashSet<Guid> allowed)
//        {
//            var raw = query["repoId"].FirstOrDefault()
//                   ?? query["Repo_Id"].FirstOrDefault()
//                   ?? query["repo_id"].FirstOrDefault();

//            if (string.IsNullOrWhiteSpace(raw)) return null; // no param → pass through

//            return Guid.TryParse(raw, out var id)
//                ? (allowed.Contains(id) ? null : $"You do not have access to repository '{raw}'.")
//                : $"Invalid repoId format: '{raw}'.";
//        }

//        private static async Task<string?> CheckBodyAsync(HttpContext ctx, HashSet<Guid> allowed)
//        {
//            ctx.Request.EnableBuffering(); // allows controller to re-read body
//            try
//            {
//                using var reader = new StreamReader(
//                    ctx.Request.Body, Encoding.UTF8,
//                    detectEncodingFromByteOrderMarks: false,
//                    bufferSize: 4096, leaveOpen: true);

//                var body = await reader.ReadToEndAsync();
//                ctx.Request.Body.Position = 0; // rewind for controller

//                if (string.IsNullOrWhiteSpace(body)) return null;

//                using var doc = JsonDocument.Parse(body);
//                var raw = ExtractRepoId(doc.RootElement);

//                if (string.IsNullOrWhiteSpace(raw)) return null; // no Repo_Id in body

//                return Guid.TryParse(raw, out var id)
//                    ? (allowed.Contains(id) ? null : $"You do not have access to repository '{raw}'.")
//                    : "Invalid Repo_Id format in request body.";
//            }
//            catch (JsonException) { return null; }   // non-JSON body (multipart)
//            catch (Exception ex)
//            {
//                Console.Error.WriteLine($"[RoleMiddleware] Body read error: {ex.Message}");
//                return null;
//            }
//        }

//        /// <summary>Reads Repo_Id from JSON body root — handles all casing your DTOs use.</summary>
//        private static string? ExtractRepoId(JsonElement root)
//        {
//            if (root.ValueKind != JsonValueKind.Object) return null;
//            foreach (var name in new[] { "Repo_Id", "repo_id", "repoId", "RepoId" })
//            {
//                if (root.TryGetProperty(name, out var p) &&
//                    p.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
//                    return p.ToString();
//            }
//            return null;
//        }

//        private static async Task Deny(HttpContext ctx, int status, string code, string msg)
//        {
//            if (ctx.Response.HasStarted) return;
//            ctx.Response.StatusCode = status;
//            ctx.Response.ContentType = "application/json";
//            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
//            {
//                ErrorCode = status,
//                Code = code,
//                ErrorMessage = msg
//            }));
//        }
//    }
//}
