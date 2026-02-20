using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        public async Task<ProcessedAttachmentResult> ProcessAndCopyAttachmentsAsync(string rawHtml, List<Tempdata> temps, string relativePermPath, Guid? entityId, string? module)
        {
            var result = new ProcessedAttachmentResult { UpdatedHtml = rawHtml ?? "" };
            if (temps == null || !temps.Any()) return result;

            var permFolderBase = _configuration["FileSettings:OriginalFolder"]; //
            var permFolder = Path.Combine(permFolderBase, relativePermPath);
            Directory.CreateDirectory(permFolder);

            var request = _httpContextAccessor.HttpContext.Request;
            string baseUrl = $"{request.Scheme}://{request.Host}";

            foreach (var file in temps)
            {
                var tempFilePath = Path.Combine(file.LocalPath);
                var permanentFilePath = Path.Combine(permFolder, file.FileName);

                try
                {
                    // 1. COPY the file (Do not move yet, in case DB fails)
                    if (File.Exists(tempFilePath))
                    {
                        File.Copy(tempFilePath, permanentFilePath, overwrite: true);
                        result.PermanentFilePathsCreated.Add(permanentFilePath); // Track for rollback
                    }

                    // 2. Build the new permanent URL
                    var newPermUrl = $"{baseUrl}/Uploads/{relativePermPath}/{file.FileName}";

                    // 3. Update the HTML replacing the Temp URL with the Permanent URL
                    // This regex safely finds the specific temp URL for this file and replaces it
                    var pattern = $@"(src|href)=[""']([^""']*)/UploadsTemp/([^""']*)/{Regex.Escape(file.FileName)}[""']";
                    result.UpdatedHtml = Regex.Replace(result.UpdatedHtml, pattern, $"$1=\"{newPermUrl}\"");

                    // 4. Create the Attachment metadata (DO NOT SAVE TO DB HERE)
                    var attachment = new AttachmentMaster
                    {
                        Id = entityId,
                        FileName = file.FileName,
                        FilePath = permanentFilePath,
                        FileType = GetMimeType(permanentFilePath),
                        FileSize = new FileInfo(permanentFilePath).Length,
                        UploadedBy = _loginContextService.userId,
                        CreatedOn = DateTime.UtcNow,
                        Status = "Active",
                        FileExtension = Path.GetExtension(file.FileName).TrimStart('.'),
                        RelativePath = $"{relativePermPath}/{file.FileName}",
                        ModuleName = module,
                    };
                    result.Attachments.Add(attachment);
                }
                catch (Exception ex)
                {
                    // If any file copy fails, rollback files immediately and bubble up exception
                    RollbackPhysicalFiles(result.PermanentFilePathsCreated);
                    throw new Exception($"Failed to process attachment {file.FileName}", ex);
                }
            }

            return result;
        }

        // Call this in your catch blocks!
        public void RollbackPhysicalFiles(List<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); }
                    catch { /* Log failure, but continue deleting others */ }
                }
            }
        }

        private string GetMimeType(string filePath)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out string mimeType))
            {
                mimeType = "application/octet-stream";
            }
            return mimeType;
        }

        public Task CleanupTempFiles(TempReturn filePaths)
        {
            if (filePaths == null || filePaths.temps == null || !filePaths.temps.Any())
                return Task.CompletedTask;

            // Normalize string (safe compare)
            var deleteMode = (filePaths.Delete ?? "").Trim().ToLower();

            // CASE 1 — Delete Single File
            if (deleteMode == "single")
            {
                var file = filePaths.temps.First();

                if (System.IO.File.Exists(file.LocalPath))
                {
                    try
                    {
                        System.IO.File.Delete(file.LocalPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting file {file.LocalPath}: {ex.Message}");
                    }
                }

                return Task.CompletedTask;
            }

            // CASE 2 — Delete All Files (delete entire folder)
            if (deleteMode == "all")
            {
                var folderPath = Path.GetDirectoryName(filePaths.temps.First().LocalPath);

                if (Directory.Exists(folderPath))
                {
                    try
                    {
                        Directory.Delete(folderPath, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting directory {folderPath}: {ex.Message}");
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
