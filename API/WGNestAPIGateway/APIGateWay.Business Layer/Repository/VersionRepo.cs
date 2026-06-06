using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class VersionRepo : IVersionRepo
    {
        private readonly APIGatewayDBContext _dbContext;
        private readonly IHubContext<RealtimeHub> _hub;
        private readonly IDomainService _domain;

        public VersionRepo(APIGatewayDBContext dBContext, IHubContext<RealtimeHub> hub, IDomainService domain) 
        {
            _dbContext = dBContext;
            _hub = hub;
            _domain = domain;
        }    

        public async Task<string> GetAppVersion()
        {
            var version =
                await _dbContext.AppSettings
                .Where(x =>
                    x.SettingKey == "APP_VERSION")
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync();

            return version;
        }
        public async Task PublishVersionAsync()
        {
            var version =
                AppVersionHelper
                    .GetCurrentVersion();

            var appVersion =
                await _dbContext.AppSettings
                    .FirstOrDefaultAsync(
                        x => x.SettingKey == "APP_VERSION");

            if (appVersion == null)
                return;

            appVersion.SettingValue = version;

            await _domain.UpdateAsync(appVersion);

            await _hub.Clients
                .All
                .SendAsync(
                    "VersionUpdated",
                    new
                    {
                        Version = version,
                        DeployedAt = appVersion.UpdatedAt
                    });
        }

        public static class AppVersionHelper
        {
            public static string GetCurrentVersion()
            {
                return Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?
                .Split('+')[0]
                ?? "1.0";
            }
        }
    }
}
