
using APIGateway.Infrastructure;
using APIGateway.Middleware;
using APIGateway.Proxy;
using APIGateway.Swagger;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.Repository;
using APIGateWay.BusinessLayer.Auth;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Helpers.ilog;
using APIGateWay.BusinessLayer.Helpers.log;
using APIGateWay.BusinessLayer.Helpers.token;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.Repository;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.Middelware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Yarp.ReverseProxy.Transforms;
using static APIGateWay.ModalLayer.Helper.HelperModal;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// Configuration
// ─────────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// ─────────────────────────────────────────────────────────────
// Controllers + JSON
// ─────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();   // your extension
builder.Services.AddSwaggerGen();

// ─────────────────────────────────────────────────────────────
// Database
// ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<APIGatewayDBContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─────────────────────────────────────────────────────────────
// Business Layer
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ILoginRepository, LoginRepository>();
builder.Services.AddScoped<IRepoRepository, RepoRepository>();
builder.Services.AddScoped<ISyncRepositoryV2, SyncRepositoryV2>();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<IAttachmentRepo, AttachmentRepo>();
builder.Services.AddScoped<IProjectRepo, ProjectRepo>();
builder.Services.AddScoped<ISyncRequestEnricher, SyncRequestEnricher>();
builder.Services.AddScoped<IDashBoardDataRepo, DashBoardDataRepo>();
builder.Services.AddScoped<IThreadsRepository, ThreadsRepository>();
builder.Services.AddScoped<ITicketRepo, TicketRepo>();

// ─────────────────────────────────────────────────────────────
// Domain Layer
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ILoginContextService, LoginContextService>();
builder.Services.AddScoped<ISyncExecutionService, SyncExecutionService>();
builder.Services.AddScoped<IRepoAccessService, RepoAccessService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IDomainService, DomainService>();
builder.Services.AddScoped<IHelperGetData, HelperGetData>();
builder.Services.AddScoped<IDashBoardDataService, DashBoardDataService>();

// ─────────────────────────────────────────────────────────────
// Infrastructure
// ─────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<APIGateWayCommonService>();
builder.Services.AddScoped<DecodeHelpers>();
builder.Services.AddScoped<IlogHelper, LogHelper>();
builder.Services.AddScoped<TokenGeneration>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddAutoMapper(config =>
    config.AddMaps(AppDomain.CurrentDomain.GetAssemblies()));
builder.Services.AddSingleton<IUserIdProvider, GuidUserIdProvider>();

// ─────────────────────────────────────────────────────────────
// HTTP Client
// ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<IRepoService, RepoService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:5070/");
});

// ─────────────────────────────────────────────────────────────
// Reverse Proxy (YARP)
// ─────────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(reqCtx =>
        {
            var svc = reqCtx.HttpContext.Request.Headers["wg_token"].ToString();
            if (!string.IsNullOrEmpty(svc))
                reqCtx.ProxyRequest.Headers.Add("X-Service-Name", svc);

            return ValueTask.CompletedTask;
        });
    })
    .LoadFromMemory(ProxyConfigBuilder.Build().Routes, ProxyConfigBuilder.Build().Clusters);

// ─────────────────────────────────────────────────────────────
// 🔐 JWT Authentication
// ─────────────────────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);

// ─────────────────────────────────────────────────────────────
// 🔐 Authorization Policy (RepoScopeHandler)
// ─────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RepoScopePolicy",
        policy => policy.Requirements.Add(new RepoScopeRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, RepoScopeHandler>();

// ─────────────────────────────────────────────────────────────
// CORS
// ─────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ─────────────────────────────────────────────────────────────
// Build App
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseRouting();
app.UseCors("FrontendPolicy");

// 🔐 Authentication & Authorization MUST be before endpoints
app.UseAuthentication();
app.UseAuthorization();


// ─────────────────────────────────────────────────────────────
// Static Files
// ─────────────────────────────────────────────────────────────
var staticFolders = builder.Configuration
    .GetSection("StaticFolders").Get<List<StaticFolderItem>>();

if (staticFolders != null)
{
    foreach (var folder in staticFolders)
    {
        if (!Directory.Exists(folder.PhysicalPath))
            Directory.CreateDirectory(folder.PhysicalPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(folder.PhysicalPath),
            RequestPath = folder.RequestPath
        });
    }
}

// Optional middlewares
app.UseMiddleware<ResponseWrappingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();


// ─────────────────────────────────────────────────────────────
// Swagger
// ─────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "APIGateway v1");
});

// ─────────────────────────────────────────────────────────────
// Endpoints
// ─────────────────────────────────────────────────────────────
app.MapHub<RealtimeHub>("/realtime").RequireAuthorization();
app.MapReverseProxy();
app.MapControllers().RequireAuthorization("RepoScopePolicy");

// ─────────────────────────────────────────────────────────────
builder.WebHost.UseUrls("https://*:8008");
app.Run();





//builder.Services.AddControllers();
//builder.Services.AddSwaggerDocumentation();
//builder.Services.AddControllers().AddJsonOptions(opt =>
//{
//    opt.JsonSerializerOptions.PropertyNamingPolicy = null;
//});
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
//builder.Services.AddDbContext<APIGatewayDBContext>(Options =>
//    Options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));



//builder.Services.AddScoped<ILoginRepository, LoginRepository>();
//builder.Services.AddScoped<IRepoRepository, RepoRepository>();
//builder.Services.AddScoped<ISyncRepositoryV2, SyncRepositoryV2>();
//builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
//builder.Services.AddScoped<IAttachmentRepo, AttachmentRepo>();
//builder.Services.AddScoped<IProjectRepo, ProjectRepo>();


//builder.Services.AddScoped<ILoginService, LoginService>();
//builder.Services.AddScoped<ILoginContextService, LoginContextService>();
//builder.Services.AddScoped<ISyncExecutionService, SyncExecutionService>();
//builder.Services.AddScoped<IRepoAccessService, RepoAccessService>();
//builder.Services.AddScoped<IAttachmentService, AttachmentService>();
//builder.Services.AddScoped<IDomainService, DomainService>();
//builder.Services.AddScoped<IHelperGetData, HelperGetData>();
//builder.Services.AddScoped<IRepoScopeValidator, RepoScopeValidator>();
//builder.Services.AddScoped<ISyncRoleGuard, SyncRoleGuard>();

//builder.Services.AddDistributedMemoryCache();
//builder.Services.AddScoped<APIGateWayCommonService>();
//builder.Services.AddScoped<DecodeHelpers>();
//builder.Services.AddScoped<IlogHelper, LogHelper>();
//builder.Services.AddHttpContextAccessor();
//builder.Services.AddScoped<TokenGeneration>();
//builder.Services.AddSignalR();
//builder.Services.AddAutoMapper(config =>
//{
//    config.AddMaps(AppDomain.CurrentDomain.GetAssemblies());
//});

//builder.Services.AddSingleton<IUserIdProvider, GuidUserIdProvider>();
//builder.Services.AddHttpClient<IRepoService, RepoService>(client =>
//{
//    client.BaseAddress = new Uri("https://localhost:5070/");
//});
//// Add YARP Reverse Proxy services
//builder.Services.AddReverseProxy()
//    .AddTransforms(transfromBuilderContext =>
//    {
//        transfromBuilderContext.AddRequestTransform(transfromContext =>
//        {
//            var httpContext = transfromContext.HttpContext;
//            var serviceName = httpContext.Request.Headers["wg_token"].ToString();

//            if (!string.IsNullOrEmpty(serviceName))
//            {
//                transfromContext.ProxyRequest.Headers.Add("X-Service-Name", serviceName);
//            }

//            return ValueTask.CompletedTask; // 🔥 Required
//        });
//    })
//    .LoadFromMemory(ProxyConfigBuilder.Build().Routes, ProxyConfigBuilder.Build().Clusters);

//builder.Services.AddAuthentication("Bearer")
//    .AddJwtBearer("Bearer", options =>
//    {
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuerSigningKey = true,
//            IssuerSigningKey = new SymmetricSecurityKey(
//                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
//            ),
//            ValidateIssuer = false,
//            ValidateAudience = false,
//            ClockSkew = TimeSpan.Zero
//        };

//        options.Events = new JwtBearerEvents
//        {
//            OnMessageReceived = context =>
//            {
//                var path = context.HttpContext.Request.Path;

//                if (path.StartsWithSegments("/realtime"))
//                {
//                    var accessToken =
//                        context.Request.Query["access_token"];

//                    if (!string.IsNullOrEmpty(accessToken))
//                    {
//                        context.Token = accessToken;
//                    }
//                }

//                return Task.CompletedTask;
//            }
//        };
//    });


//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("FrontendPolicy", builder =>
//    {
//        builder.WithOrigins("http://localhost:5173") // exact frontend origin
//              .AllowAnyHeader()
//              .AllowAnyMethod()
//              .AllowCredentials(); // 🔥 REQUIRED FOR SIGNALR
//    });
//});

////builder.Services.AddCors(options =>
////{
////    options.AddPolicy("AllowAll", builder =>
////    {
////        builder
////                .AllowAnyOrigin()
////               .AllowAnyMethod()
////               .AllowAnyHeader();
////        //.AllowCredentials();
////    });
////});
//var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
//    app.UseDeveloperExceptionPage();
//}
//app.UseRouting();
//app.UseCors("FrontendPolicy");

//// --- SERVE STATIC FILES FROM D: DRIVE ---
//var staticFolders = builder.Configuration.GetSection("StaticFolders").Get<List<StaticFolderItem>>();
//if (staticFolders != null)
//{
//    foreach (var folder in staticFolders)
//    {
//        if (!Directory.Exists(folder.PhysicalPath)) Directory.CreateDirectory(folder.PhysicalPath);

//        app.UseStaticFiles(new StaticFileOptions
//        {
//            FileProvider = new PhysicalFileProvider(folder.PhysicalPath),
//            RequestPath = folder.RequestPath,
//            OnPrepareResponse = ctx =>
//            {
//                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
//                ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
//                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "Get, OPTIONS");
//            }
//        });
//    }
//}
//app.UseAuthentication();
//app.UseAuthorization();
//app.MapHub<RealtimeHub>("/realtime").RequireAuthorization();
//app.UseMiddleware<ResponseWrappingMiddleware>();
//app.UseMiddleware<ErrorHandlingMiddleware>();
//app.UseMiddleware<HttpContextMiddleware>();
//app.UseMiddleware<TokenValidationAuth>();

//app.UseSwagger();
//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DentalApp");
//});
//// Enable YARP Reverse Proxy middleware
//app.MapReverseProxy();

//app.MapControllers();

//builder.WebHost.UseUrls("https://*:8008");

//app.Run();
