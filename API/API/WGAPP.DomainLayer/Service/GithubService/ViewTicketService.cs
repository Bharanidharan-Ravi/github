using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.ErrorException;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.DomainLayer.Service.GithubService
{
    public class ViewTicketService : IViewTicketService
    {
        private readonly WGAPPCommonService _commonService;
        private readonly WGAPPDbContext _DbContext;
        private readonly ILoginContextService _loginContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ViewTicketService(WGAPPCommonService commonService, WGAPPDbContext WGAPPDbContext, ILoginContextService loginContext, IHttpContextAccessor httpContextAccessor)
        {
            _commonService = commonService;
            _DbContext = WGAPPDbContext;
            _loginContext = loginContext;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<ThreadbyTicketId> GetThreadData(Guid IssuesId)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DbName", _loginContext.databaseName),
                new SqlParameter("@IssuesId", IssuesId)
            };
            var dataSet = await _commonService.ExecuteReturnAsync("GETTHREADLIST", parameters);
            if (dataSet == null || dataSet.Tables.Count < 1 || dataSet.Tables[0].Rows.Count == 0)
                throw new Exception("Doctor not found.");

            // Deserialize results
            var result = new ThreadbyTicketId
            {
                issuesData = dataSet.Tables[0].Rows[0].AutoCast<GetAllIssueData>(),
                threadData = dataSet.Tables[1].AsEnumerable().Select(row => row.AutoCast<ThreadCommentDto>()).ToList(),
            };

            // ---------------------------------
            // 1️⃣ PROCESS ISSUE ATTACHMENTS
            // ---------------------------------
            if (result.issuesData.Attachment_JSON != null && result.issuesData.Attachment_JSON.Any())
            {
                //result.issuesData.Attachment_JSON =
                //    JsonConvert.DeserializeObject<List<AttachmentMaster>>(result.issuesData.Attachment_JSON);

                foreach (var file in result.issuesData.Attachment_JSON)
                {
                    file.PublicUrl = GeneratePreviewUrl(file.RelativePath);
                }
            }

            // ---------------------------------
            // 2️⃣ PROCESS THREAD ATTACHMENTS
            // ---------------------------------
            foreach (var thread in result.threadData)
            {
                if (thread.Attachment_JSON != null && thread.Attachment_JSON.Any())
                {
                    //thread.Attachment_JSON =
                    //    JsonConvert.DeserializeObject<List<AttachmentMaster>>(thread.Attachment_JSON);

                    foreach (var file in thread.Attachment_JSON)
                    {
                        file.PublicUrl = GeneratePreviewUrl(file.RelativePath);
                    }
                }
            }

            return result;
        }


        public string GeneratePreviewUrl(string filePath)
        {
            // Define the base URL that corresponds to where your images are publicly accessible
            var request = _httpContextAccessor.HttpContext.Request;
            string baseUrl = $"{request.Scheme}://{request.Host}";

            // Example: G:\WG-W1 DATA\TestImage\original\9a3e09b8... -> 9a3e09b8...
            string fileName = Path.GetFileName(filePath); /// Extract the file name from the full path
            var PublicUrl = $"{baseUrl}/Uploads/{filePath}";
            // Create the public URL by combining the base URL with the file name
            return PublicUrl;
        }
    }

}
