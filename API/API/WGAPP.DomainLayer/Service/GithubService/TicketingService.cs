using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Data;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.ErrorException;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.ModelLayer;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.DomainLayer.Service.GithubService
{
    public class TicketingService : ITicketingService
    {
        private readonly WGAPPDbContext _context;
        private readonly WGAPPCommonService _commonservice;
        private readonly ILoginContextService _loginService;
        private readonly IViewTicketService _viewTicketService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TicketingService(WGAPPDbContext context, WGAPPCommonService wGAPPCommonService, ILoginContextService loginService, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IViewTicketService viewTicketService)
        {
            _context = context;
            _commonservice = wGAPPCommonService;
            _loginService = loginService;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _viewTicketService = viewTicketService;
        }

        #region insert a value into issue master 
        public async Task<GetAllIssueData> AddIssuesAsync(IssueMaster issue, List<ISSUE_LABELS> labels, TempReturn temp)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
   
            try
            {
                var userId = _loginService.userId;
                var PermuserId = $"{userId}-{_loginService.userName}";

                // Step 1: Create Ticket
                issue.Issuer_Id = userId;
                issue.Created_On = DateTime.UtcNow;
                issue.Status = "Active";
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@SeriesName", "IssueMaster"),
                };
                var sino = await _commonservice.ExecuteGetItemAsyc<SequenceResult>("GetNextNumber", parameters);
                int value = sino[0].CurrentValue;
                issue.SiNo = value;

                _context.IssueMasters.Add(issue);
                await _context.SaveChangesAsync();

               
                
                Guid? ticketId = issue.Issue_Id;
                var permTicket = $"{ticketId}-{issue.Title}";
                var Relative = $"{PermuserId}/{permTicket}";
               
                var (success, attachmentId) = await MoveFilesToPermanentAsync(temp.temps, ticketId,0, Relative);

                if (!success)
                {
                    // If any file move operation fails, rollback the transaction
                    await transaction.RollbackAsync();
                    return null; // Or handle the error accordingly
                }
                if (labels != null && labels.Count > 0)
                {
                    // Update Label entries with the correct Issue_Id (ticketId)
                    foreach (var label in labels)
                    {
                        label.Label_Id = label.Label_Id;
                        label.Issue_Id = ticketId.Value; // Assign the ticket Id to the label
                    }

                    _context.Labels.AddRange(labels); // Add all labels at once
                }
                // Step 4: Commit transaction
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
               
                //var Pathname = temps.PathName;
                // Step 5: Cleanup temp folder
                await CleanupTempFiles(temp);
                var response = await GetIssuesById(issue.Issue_Id);
                return response[0];
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("error while creating a tickets", ex);
            }
        }
        #region Attachment Funtion 
        public async Task<(bool Success, int AttachmentId)> MoveFilesToPermanentAsync(List<Tempdata> temps, Guid? ticketId,int threadId, string Relative)
        {
            // Step 2: Move files Temp → Permanent
            var permFolder = Path.Combine(_configuration["FileSettings:OriginalFolder"], Relative);
            Directory.CreateDirectory(permFolder);
            int AttachmentId = 0;
            var Pathname = new List<string>();
            foreach (var files in temps)
            {
                var tempFilePath = Path.Combine(files.LocalPath);
                var permanentFilePath = Path.Combine(permFolder, files.FileName);

                try
                {
                    // Move file from temp to permanent location
                    File.Move(tempFilePath, permanentFilePath);

                    // Get MIME type of the file
                    var fileType = GetMimeType(permanentFilePath);

                    // Insert file metadata into the database
                    var attachment = new AttachmentMaster
                    {
                        TicketId = ticketId,
                        FileName = files.FileName,
                        FilePath = permanentFilePath,
                        FileType = fileType, // Dynamically set the file type
                        FileSize = new FileInfo(permanentFilePath).Length,
                        UploadedBy = _loginService.userId,
                        CreatedOn = DateTime.UtcNow,
                        Status = "Active",
                        FileExtension = Path.GetExtension(files.FileName).TrimStart('.'),
                        RelativePath = $"{Relative}/{files.FileName}",
                    };
                    if (threadId != 0)
                    {
                        attachment.ThreadId = threadId;
                    }

                    _context.AttachmentMasters.Add(attachment);
                    await _context.SaveChangesAsync();
                    AttachmentId = attachment.AttachmentId;
                    //Pathname.Add(tempFilePath);
                }
                catch
                {
                    // If any move fails, return false
                    return (true, AttachmentId);
                }
            }

            return (true, AttachmentId);
        }
        private string GetMimeType(string filePath)
        {
            var provider = new FileExtensionContentTypeProvider();
            string mimeType;

            // Get MIME type based on file extension
            if (!provider.TryGetContentType(filePath, out mimeType))
            {
                mimeType = "application/octet-stream"; // Default type for unknown file types
            }

            return mimeType;
        }
        #endregion

        public async Task<List<GetAllIssueData>> GetIssuesById(Guid? issueId = null, Guid? ProjectId = null)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DbName", _loginService.databaseName),
                new SqlParameter("@IssueId", issueId ?? (object)DBNull.Value),
                new SqlParameter("@ProjectId", ProjectId ?? (object)DBNull.Value)
            };
            //var response = await _commonservice.ExecuteGetItemAsyc<GetAllIssueData>("GETISSUSEBYID", parameters);
            var response = await _commonservice.ExecuteReturnAsync("GETISSUSEBYID", parameters);
            var result = response.Tables[0].AsEnumerable().Select(row => row.AutoCast<GetAllIssueData>()).ToList();
            foreach (var thread in result)
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
        #endregion

        #region insert a value into issue thread 
        public async Task<ThreadbyTicketId> AddIssueThreadAsync(IssuesThread thread, TempReturn temp)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var userId = _loginService.userId;
                var PermuserId = $"{userId}-{_loginService.userName}";

                var parameters = new SqlParameter[]
                {
                new SqlParameter("@SeriesName", "ISSUETHREADS"),
                };
                var sino = await _commonservice.ExecuteGetItemAsyc<SequenceResult>("GetNextNumber", parameters);
                int value = sino[0].CurrentValue;

                thread.CommentedBy = userId;
                thread.CommentedAt = DateTime.UtcNow;
                thread.ThreadId = value;

                _context.ISSUETHREADS.Add(thread);


                Guid? ticketId = thread.Issue_Id;
                var permTicket = $"{ticketId}-{thread.IssueTitle}";
                var Relative = $"{PermuserId}/{permTicket}";

                var (success, attachmentId) = await MoveFilesToPermanentAsync(temp.temps, ticketId, value, Relative);

                if (!success)
                {
                    // If any file move operation fails, rollback the transaction
                    await transaction.RollbackAsync();
                    return null; // Or handle the error accordingly
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                await CleanupTempFiles(temp);
                var response = await _viewTicketService.GetThreadData(thread.Issue_Id);

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("error while creating a tickets", ex);
            }
        }
        #endregion

        #region get a old issues for update a issues master 
        public async Task<IssueMaster> GetIssueByRepoAndIssueIdAsync(Guid repoId, Guid issueId)
        {
            return await _context.IssueMasters
                .FirstOrDefaultAsync(i => i.Repo_Id == repoId && i.Issue_Id == issueId);
        }
        #endregion

        #region Update a issues master 
        public async Task<IssueMaster> UpdateIssueAsync(IssueMaster issue)
        {
            _context.IssueMasters.Update(issue);
            await _context.SaveChangesAsync();
            return issue;
        }
        #endregion

        #region Get a all issue master based on repo
        public async Task<List<GetAllIssueData>> GetAllIssueData(Guid? clientId = null, string FilterBy = null)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DbName", _loginService.databaseName),
                new SqlParameter("@ClientId", clientId ?? (object)DBNull.Value),
                new SqlParameter("@FilterBy", FilterBy ?? (object)DBNull.Value),
            };
            var dataSet = await _commonservice.ExecuteReturnAsync("GetAllIssuesData", parameters);
            var issuesTable = dataSet.Tables[0]; // Assuming the first table contains the issues data

            // Convert the DataTable into a list of GetAllIssueData objects
            var issueDatas = issuesTable.AsEnumerable()
                .Select(row => new GetAllIssueData
                {
                    Issue_Id = row.Field<Guid>("Issue_Id"),
                    Issue_Title = row.Field<string>("Issue_Title"),
                    Description = row.Field<string>("Description"),
                    Issuer_Id = row.Field<Guid?>("Issuer_Id"),
                    Issuer_Name = row.Field<string>("Issuer_Name"),
                    Created_On = row.Field<DateTime>("Created_On"),
                    Project_Id = row.Field<Guid>("Project_Id"),
                    Assignee_Id = row.Field<Guid?>("Assignee_Id"),
                    Assignee_Name = row.Field<string>("Assignee_Name"),
                    Due_Date = row.Field<DateTime?>("Due_Date"),
                    Status = row.Field<string>("Status"),
                    Issue_Code = row.Field<string>("Issue_Code"),
                    Updated_On = row.IsNull("Updated_On") ? (DateTime?)null : row.Field<DateTime>("Updated_On"),
                    Updated_By = row.Field<Guid?>("Updated_By"),

                    // Deserialize JSON fields (Labels and Attachments)
                    Labels_JSON = DeserializeSafeJson<List<GETLABELFORISSUES>>(row.Field<string>("Labels_JSON")),
                    Attachment_JSON = DeserializeSafeJson<List<GETATTACHFORISSUES>>(row.Field<string>("Attachment_JSON"))
                })
                .ToList();
            // Generate Preview URLs for each attachment
            foreach (var issue in issueDatas)
            {
                foreach (var attachment in issue.Attachment_JSON)
                {
                    // Generate the Preview URL for each attachment
                    attachment.RelativePath = GeneratePreviewUrl(attachment.RelativePath);
                }
            }
            if (issueDatas == null)
            {
                throw new Exceptionlist.DataNotFoundException("No data found for the provided parameters.");
            }
            return issueDatas;
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
        public static T DeserializeSafeJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Activator.CreateInstance<T>();

            try
            {
                return JsonConvert.DeserializeObject<T>(json) ?? Activator.CreateInstance<T>();
            }
            catch (JsonException)
            {
                return Activator.CreateInstance<T>();
            }
        }
        #endregion

        #region Get a all issue master 
        public async Task<List<GetAllIssueData>> GetMasterIssueData()
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@DatabaseName", _loginService.databaseName),
                new SqlParameter("@UserId", _loginService.userId)
            };
            var IssuesData = await _commonservice.ExecuteGetItemAsyc<GetAllIssueData>("GetIssuesByUserId", parameters);
            if (IssuesData == null || IssuesData.Count == 0)
            {
                throw new Exceptionlist.DataNotFoundException("No data found for the provided parameters.");
            }
            return IssuesData;
        }
        #endregion

        #region Post a attachment file
        public async Task<Tempdata> UploadFilesToTempAsync(IFormFile files)
        {
            if (files == null)
                throw new Exception("Invaild file");
            var userId = $"{_loginService.userId}-{_loginService.userName}";

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
                {
                    // If any file fails, delete temp folder
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);

                    throw new Exception("failed to add a file", ex);
                }
            }
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

        #endregion


    }
}