

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



builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IRepoService, RepoService>();
builder.Services.AddScoped<ILoginContextService, LoginContextService>();


builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<APIGateWayCommonService>();
builder.Services.AddScoped<DecodeHelpers>();
builder.Services.AddScoped<IlogHelper, LogHelper>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TokenGeneration>();
builder.Services.AddHttpClient<IRepoService, RepoService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:5070/");
});
// Add YARP Reverse Proxy services
builder.Services.AddReverseProxy()
    .AddTransforms(transfromBuilderContext =>
    {
        transfromBuilderContext.AddRequestTransform(async transfromContext =>
        {
            // Access the HttpContext here
            var httpContext = transfromContext.HttpContext;
            var serviceName = httpContext.Request.Headers["wg_token"].ToString();
            if (!string.IsNullOrEmpty(serviceName))
            {
                transfromContext.ProxyRequest.Headers.Add("X-Service-Name", serviceName);
            }
        });
    })
    .LoadFromMemory(ProxyConfigBuilder.Build().Routes, ProxyConfigBuilder.Build().Clusters);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseRouting();
app.UseCors("AllowAll");

app.UseMiddleware<ResponseWrappingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<HttpContextMiddleware>();
app.UseMiddleware<TokenValidationAuth>();


app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DentalApp");
});
// Enable YARP Reverse Proxy middleware
app.MapReverseProxy();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});
builder.WebHost.UseUrls("https://*:8008");

app.Run();
