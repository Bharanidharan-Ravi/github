using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class AttachmentService : IAttachmentService
    {
        private readonly ILoginContextService _loginContextService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AttachmentService (ILoginContextService loginContextService,IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _loginContextService = loginContextService;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        #region Post a attachment file
        public async Task<Tempdata> UploadFilesToTempAsync(IFormFile files)
        {
            if (files == null)
                throw new Exception("Invaild file");
            var userId = $"{_loginContextService.userId}-{_loginContextService.userName}";

            var rootFolder = _configuration["FileSettings:TempFolder"];
            var tempFolder = Path.Combine(rootFolder, userId);
            Directory.CreateDirectory(tempFolder);

            //var tempFileNames = new List<string>();

            try
            {
                //foreach (var file in files)
                //{
                var fileName = Path.GetFileName(files.FileName);
                var filePath = Path.Combine(tempFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await files.CopyToAsync(stream);

                var request = _httpContextAccessor.HttpContext.Request;
                string baseUrl = $"{request.Scheme}://{request.Host}";

                var response = new Tempdata
                {
                    FileName = fileName,
                    PublicUrl = $"{baseUrl}/UploadsTemp/{userId}/{fileName}",
                    LocalPath = filePath,
                };

                return (response);
            }
            catch (Exception ex)
            {
                // If any file fails, delete temp folder
                if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);

                throw new Exception("failed to add a file", ex);
                
            }
        }
        #endregion
    }
}
