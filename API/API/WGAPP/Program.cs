using WGAPP.BusinessLayer.Interface;
using WGAPP.BusinessLayer.Repository;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.Interface;
using WGAPP.DomainLayer.Service;
using WGAPP.DomainLayer.Services.CommonServices;
using Microsoft.EntityFrameworkCore;
using WGAPP.Swagger;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.Middelware;
using WGAPP.BusinessLayer.Helpers.ilog;
using WGAPP.BusinessLayer.Helpers.log;
using WGAPP.BusinessLayer.Helpers.token;
using WGAPP.BusinessLayer.Helpers;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.BusinessLayer.Repository.GithubRepository;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Service.GithubService;
using WGAPP.BusinessLayer.Hub;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Services.AddControllers();
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    // This prevents the default camelCase conversion
    options.PayloadSerializerOptions.PropertyNamingPolicy = 
    System.Text.Json.JsonNamingPolicy.CamelCase;
});
// Add Swagger documentation
builder.Services.AddSwaggerDocumentation();
var uploadsPath = builder.Configuration["FileSettings:TempFolder"];
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<WGAPPDbContext>(Options =>
    Options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ILoginRepository, LoginRepository>();
builder.Services.AddScoped<ITicketingRepository, TicketingRepository>();
builder.Services.AddScoped<IViewTicketRepository, ViewTicketRepository>();
builder.Services.AddScoped<IRepositoryInterface, RepositoryRepo>();
builder.Services.AddScoped<IMasterDataRepo, MasterDataRepo>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<ITicketingService, TicketingService>();
builder.Services.AddScoped<IViewTicketService, ViewTicketService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<IProjectService, ProjectService>();

builder.Services.AddScoped<ILoginContextService, LoginContextService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<WGAPPCommonService>();
builder.Services.AddScoped<IlogHelper, LogHelper>();
builder.Services.AddScoped<DecodeHelpers>();
builder.Services.AddHttpContextAccessor();
//builder.Services.AddScoped<LogHelper>();
builder.Services.AddScoped<TokenGeneration>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
                .AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
               //.AllowCredentials();
    });
});

var app = builder.Build();
app.MapHub<NotificationHub>("/hubs/notifications");
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseCors("AllowAll");
//app.Use(async (context, next) =>
//{
//    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
//    context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS";
//    context.Response.Headers["Access-Control-Allow-Headers"] = "*";

//    // Handle preflight requests early
//    if (context.Request.Method == "OPTIONS")
//    {
//        context.Response.StatusCode = 204;
//        return;
//    }

//    await next();
//});
//app.UseStaticFiles(new StaticFileOptions
//{
//    FileProvider = new PhysicalFileProvider(uploadsPath),
//    RequestPath = "/MyUploads",
//    OnPrepareResponse = ctx =>
//    {
//        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
//        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
//        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
//    }
//});

//app.UseStaticFileConfig(builder.Configuration);

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<HttpContextMiddleware>();
//app.UseMiddleware<TokenValidationMiddleware>();


app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "WGApp");
});


app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
}); 
app.MapControllers();
//builder.WebHost.UseUrls("https://*:8005");

app.Run();
