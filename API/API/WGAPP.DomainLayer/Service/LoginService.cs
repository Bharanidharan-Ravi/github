using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.DomainLayer.ErrorException;
using WGAPP.DomainLayer.Interface;
using WGAPP.ModelLayer;

namespace WGAPP.DomainLayer.Service
{
    public class LoginService : ILoginService
    {
        private readonly WGAPPDbContext _context;
        private readonly WGAPPCommonService _commonService;
        private readonly IConfiguration _configuration;


        public static readonly Dictionary<int, string> _activeJwtTokens = new Dictionary<int, string>();

        public LoginService(WGAPPDbContext context, WGAPPCommonService commonService, IConfiguration configuration)
        {
            _context = context;
            _commonService = commonService;
            _configuration = configuration;

            /*_connectionHelper = new ConnectionHelper(configuration);*/
        }

        public async Task<List<GetUserModel>> GetUser(string username, string password, string deviceInfo)
        {
            var parameters = new SqlParameter[]
            {
                //new SqlParameter("@NAME", groupName),
                new SqlParameter("@username", username),
                new SqlParameter("@password", password)
            };
            //var user = await _context.WGReloadUser.FirstOrDefaultAsync(u => u.D_UserName == username && u.D_Password == password);
            var userList = await _commonService.ExecuteGetItemAsyc<GetUserModel>("validateuser", parameters);
            if (userList == null || userList.Count == 0)
            {
                throw new Exceptionlist.LoginException("No Valid User.", username, deviceInfo, password);
            }
            var user = userList.FirstOrDefault();

            await SaveUserSession(user.UserId, user.UserName, user.DBName, deviceInfo, DateTime.Now, "0");

            return userList;
        }

        private async Task SaveUserSession(Guid userId, string userName, string databaseName, string deviceInfo, DateTime loginTimestamp, string autoLogout)
        {
            var parameters = new[]
            {
                new SqlParameter("@userid", userId),
                new SqlParameter("@username", userName),
                new SqlParameter("@company", "companyName"),
                new SqlParameter("@database", databaseName),
                new SqlParameter("@device", deviceInfo),
                new SqlParameter("@login", loginTimestamp),
                new SqlParameter("@logout", DBNull.Value)
            };

            // Execute the stored procedure INSERTUSERLOG
            //await _commonService.ExecuteNonModalAsync("InsertUserlog", parameters);
        }
    }
}
