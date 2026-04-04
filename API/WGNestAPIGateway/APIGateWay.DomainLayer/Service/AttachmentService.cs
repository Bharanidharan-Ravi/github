using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Mail;
using System.Text.RegularExpressions;
using static APIGateWay.ModalLayer.Helper.HelperModal;

namespace APIGateWay.DomainLayer.Service
{
    public class AttachmentService : IAttachmentService
    {
        private readonly ILoginContextService _loginContextService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly APIGateWayCommonService _commonService;
        private readonly APIGatewayDBContext _db;
        public AttachmentService (ILoginContextService loginContextService,IConfiguration configuration, IHttpContextAccessor httpContextAccessor, APIGateWayCommonService aPIGateWay, APIGatewayDBContext db)
        {
            _loginContextService = loginContextService;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _commonService = aPIGateWay;
            _db = db;
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

        public async Task<ProcessedAttachmentResult> ProcessAndCopyAttachmentsAsync(string rawHtml, List<Tempdata> temps, string relativePermPath, string? entityId, string module)
        {
            var result = new ProcessedAttachmentResult { UpdatedHtml = rawHtml ?? "" };
            if (temps == null || !temps.Any()) return result;
            relativePermPath = string.Join("/", relativePermPath.Split('/').Select(folder => folder.Trim()));
            var permFolderBase = _configuration["FileSettings:OriginalFolder"];
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
                    // 1. COPY the file
                    if (File.Exists(tempFilePath))
                    {
                        File.Copy(tempFilePath, permanentFilePath, overwrite: true);
                        result.PermanentFilePathsCreated.Add(permanentFilePath);
                    }

                    // 2. Build the new permanent URL (EscapeDataString converts spaces to %20 so HTML doesn't break)
                    var encodedFileName = Uri.EscapeDataString(file.FileName);
                    var newPermUrl = $"{baseUrl}/Uploads/{relativePermPath}/{encodedFileName}";

                    // 3. SAFELY Update the HTML
                    // Method A: Direct Replace using the exact Temp PublicUrl
                    result.UpdatedHtml = result.UpdatedHtml.Replace(file.PublicUrl, newPermUrl, StringComparison.OrdinalIgnoreCase);

                    // Method B: Robust Regex Fallback (Catches relative URLs and spaces/%20 variations)
                    var pattern = $@"(src|href)=[""']([^""']*)/UploadsTemp/([^""']*)/({Regex.Escape(file.FileName)}|{Regex.Escape(encodedFileName)})[""']";
                    result.UpdatedHtml = Regex.Replace(result.UpdatedHtml, pattern, $"$1=\"{newPermUrl}\"", RegexOptions.IgnoreCase);
                    string usersSeries = "Attachment";

                    var pUserSeries = new SqlParameter("@SeriesName", usersSeries);

                    var nextUserSeq = await _commonService
                        .ExecuteGetItemAsyc<SequenceResult>(
                            "GetNextNumber",
                            pUserSeries
                        );
                    // 4. Create the Attachment metadata (DO NOT SAVE TO DB HERE)
                    var attachment = new AttachmentMaster
                    {
                        AttachmentId= nextUserSeq[0].CurrentValue,
                        ModuleId = entityId,
                        Module = module,
                        FileName = file.FileName,
                        FilePath = permanentFilePath,
                        FileType = GetMimeType(permanentFilePath),
                        FileSize = new FileInfo(permanentFilePath).Length,
                        //CreatedBy = _loginContextService.userId,
                        //CreatedAt = DateTime.UtcNow,
                        //UpdatedBy = _loginContextService.userId,
                        //UpdatedAt = DateTime.UtcNow,
                        Status = "Active",
                        FileExtension = Path.GetExtension(file.FileName).TrimStart('.'),
                        RelativePath = $"{relativePermPath}/{file.FileName}",
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
 
        #region Rollback Physical Files (Failsafe)
        // 🔥 The Rollback Method: Called by Business Layer catch blocks or internal catch blocks
        public void RollbackPhysicalFiles(List<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any())
                return;

            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        // Log failure, but continue deleting the other files in the list
                        Console.WriteLine($"Failed to rollback file {path}: {ex.Message}");
                    }
                }
            }
        }
        #endregion
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
        public async Task Upload(DBAttachment file)
        {
            try
            {
                _db.DBAttachment.Add(file);

                // IF THIS LINE IS MISSING, THE TABLE WILL BE EMPTY!
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving to DB: {ex.Message}");
            }
        }
        public async Task<DBAttachment> GetAttachmentAsync(int id)
        {
            try
            {
                // FindAsync is the fastest way to look up a record by its Primary Key
                var attachment = await _db.DBAttachment.FindAsync(id);
                return attachment;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving file from DB: {ex.Message}");
            }
        }
    }
}
