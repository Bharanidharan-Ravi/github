

using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.BusinessLayer.Repository;
using APIGateway.Swagger;
using APIGateWay.DomainLayer.DBContext;
using Microsoft.EntityFrameworkCore;
using APIGateWay.BusinessLayer.Helpers.ilog;
using APIGateWay.BusinessLayer.Helpers.log;
using APIGateWay.BusinessLayer.Helpers.token;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.Middelware;
using APIGateway.Middleware;
using APIGateway.Auth;
using APIGateway.Proxy;
using Yarp.ReverseProxy.Transforms;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using APIGateWay.BusinessLayer.SignalRHub;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.FileProviders;
using static APIGateWay.ModalLayer.Helper.HelperModal;
using Microsoft.Extensions.DependencyInjection;
using APIGateWay.Business_Layer.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = null;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<APIGatewayDBContext>(Options =>
    Options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));



builder.Services.AddScoped<ILoginRepository, LoginRepository>();
builder.Services.AddScoped<IRepoRepository, RepoRepository>();
builder.Services.AddScoped<ISyncRepositoryV2, SyncRepositoryV2>();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<IAttachmentRepo, AttachmentRepo>();
builder.Services.AddScoped<IProjectRepo, ProjectRepo>();


builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ILoginContextService, LoginContextService>();
builder.Services.AddScoped<ISyncExecutionService, SyncExecutionService>();
builder.Services.AddScoped<IRepoAccessService, RepoAccessService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IDomainService, DomainService>();
builder.Services.AddScoped<IHelperGetData, HelperGetData>();
builder.Services.AddScoped<IRepoScopeValidator, RepoScopeValidator>();
builder.Services.AddScoped<ISyncRoleGuard, SyncRoleGuard>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<APIGateWayCommonService>();
builder.Services.AddScoped<DecodeHelpers>();
builder.Services.AddScoped<IlogHelper, LogHelper>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenGeneration>();
builder.Services.AddSignalR();
builder.Services.AddAutoMapper(config =>
{
    config.AddMaps(AppDomain.CurrentDomain.GetAssemblies());
});

builder.Services.AddSingleton<IUserIdProvider, GuidUserIdProvider>();
builder.Services.AddHttpClient<IRepoService, RepoService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:5070/");
});
// Add YARP Reverse Proxy services
builder.Services.AddReverseProxy()
    .AddTransforms(transfromBuilderContext =>
    {
        transfromBuilderContext.AddRequestTransform(transfromContext =>
        {
            var httpContext = transfromContext.HttpContext;
            var serviceName = httpContext.Request.Headers["wg_token"].ToString();

            if (!string.IsNullOrEmpty(serviceName))
            {
                transfromContext.ProxyRequest.Headers.Add("X-Service-Name", serviceName);
            }

            return ValueTask.CompletedTask; // 🔥 Required
        });
    })
    .LoadFromMemory(ProxyConfigBuilder.Build().Routes, ProxyConfigBuilder.Build().Clusters);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
            ),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;

                if (path.StartsWithSegments("/realtime"))
                {
                    var accessToken =
                        context.Request.Query["access_token"];

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", builder =>
    {
        builder.WithOrigins("http://localhost:5173") // exact frontend origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // 🔥 REQUIRED FOR SIGNALR
    });
});

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll", builder =>
//    {
//        builder
//                .AllowAnyOrigin()
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//        //.AllowCredentials();
//    });
//});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseRouting();
app.UseCors("FrontendPolicy");

// --- SERVE STATIC FILES FROM D: DRIVE ---
var staticFolders = builder.Configuration.GetSection("StaticFolders").Get<List<StaticFolderItem>>();
if (staticFolders != null)
{
    foreach (var folder in staticFolders)
    {
        if (!Directory.Exists(folder.PhysicalPath)) Directory.CreateDirectory(folder.PhysicalPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(folder.PhysicalPath),
            RequestPath = folder.RequestPath,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "Get, OPTIONS");
            }
        });
    }
}
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<RealtimeHub>("/realtime").RequireAuthorization();
app.UseMiddleware<ResponseWrappingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<HttpContextMiddleware>();
app.UseMiddleware<TokenValidationAuth>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DentalApp");
});
// Enable YARP Reverse Proxy middleware
app.MapReverseProxy();

app.MapControllers();

builder.WebHost.UseUrls("https://*:8008");

app.Run();
