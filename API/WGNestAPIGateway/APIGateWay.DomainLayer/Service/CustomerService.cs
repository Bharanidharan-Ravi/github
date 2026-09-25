using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Utilities;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.HelperModal;

namespace APIGateWay.DomainLayer.Service
{
    public class CustomerService : ICustomersService
    {

        private readonly APIGatewayDBContext _context;
        private readonly ILoginContextService _loginContext;
        private readonly ILoginService _loginService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IHelperGetData _helperGet;
        private readonly IAttachmentService _attachmentService;
        private readonly GenerateHelper _generateHelper;

        public CustomerService(APIGatewayDBContext dBContext, ILoginContextService contextService, ILoginService login, APIGateWayCommonService commonService, IHelperGetData helperGet, IAttachmentService attachmentService, GenerateHelper generateHelper)
        {
            _context = dBContext;
            _loginContext = contextService;
            _loginService = login;
            _commonService = commonService;
            _helperGet = helperGet;
            _attachmentService = attachmentService;
            _generateHelper = generateHelper;

        }
        private async Task ValidateCustomerFields(
            string? phoneNumber,
            string? mailId,
            string? customerName,
            string? excludeRepoKey = null)
        {
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                var phoneExists = await _context.RepoUsers
                    .AnyAsync(x => x.PhoneNumber == phoneNumber
                    && (excludeRepoKey == null || x.RepoKey != excludeRepoKey));

                if (phoneExists)
                    throw new Exception($"Phone number '{phoneNumber}' already exists.");
            }
            if (!string.IsNullOrEmpty(customerName))
            {
                var nameExists = await _context.RepoUsers
                    .AnyAsync(x => x.UserName == customerName
                    && (excludeRepoKey == null || x.RepoKey != excludeRepoKey));

                if (nameExists)
                    throw new Exception($"Customer Namw '{customerName}' already exists.");
            }
            if (!string.IsNullOrEmpty(mailId))
            {
                var mailExists = await _context.RepoUsers
                    .AnyAsync(x => x.MailId == mailId
                    && (excludeRepoKey == null || x.RepoKey != excludeRepoKey));

                if (mailExists)
                    throw new Exception($"Mail Id '{mailId}' already exists.");
            }

        }


        public async Task<GetCustomerDto> PostCustomer(PostCustomerDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            var customerUserId = Guid.NewGuid();
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

                string secureRepoKey = await _helperGet.GetRepoKeyByIdAsync(dto.Repo_Id.Value);
                var existing = await _context.LOGIN_MASTER.FirstOrDefaultAsync(x => x.UserName == dto.UserName);


                if (existing != null)
                {
                    throw new Exception(
                        $"{dto.UserName} already exists"
                    );
                }

                await ValidateCustomerFields(
                    dto.PhoneNumber,
                    dto.MailId,
                    dto.CustomerName);

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                    var permFolder = $"Customer-{customerUserId}";
                    var relativePath = $"{permUserId}/{permFolder}";

                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        "Customer",
                        dto.temp.temps,
                        relativePath,
                        customerUserId.ToString(),
                        "Customer"
                        );
                }

                var (hash, salt) =
                    _loginService.HashPasswordAgron(
                        dto.Password
                    );

                var newUser = new LOGIN_MASTER
                {
                    UserID = customerUserId,
                    UserName = dto.UserName,
                    PasswordHash = hash,
                    Salt = salt,
                    DBName = _loginContext.databaseName,
                    Password = dto.Password,
                    Status = "Active",
                    Role = dto.Role,
                    ClientId = null,
                };

                _context.LOGIN_MASTER.Add(newUser);
                await _context.SaveChangesAsync();
                string usersSeries = "RepostoryUserlist";
                var pUserSeries = new SqlParameter("@SeriesName", usersSeries);
                var nextUserSeq = await _commonService
                    .ExecuteGetItemAsyc<SequenceResult>(
                    "GetNextNumber",
                    pUserSeries
                    );


                var repoUser = new RepoUserList
                {
                    SiNo = nextUserSeq[0].CurrentValue,
                    UserName = dto.CustomerName,
                    MailId = dto.MailId,
                    UserId = newUser.UserID,
                    PhoneNumber = dto.PhoneNumber,
                    RepoKey = secureRepoKey,
                    Status = "Active",


                };

                _context.RepoUsers.Add(repoUser);
                await _context.SaveChangesAsync();

                if (attachmentResult?.Attachments?.Any() == true)
                {
                    await _context.AttachmentMaster.AddRangeAsync(attachmentResult.Attachments);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                string? previewUrl = null;
                var savedAvatar = attachmentResult?.Attachments?.FirstOrDefault();
                if (savedAvatar != null && !string.IsNullOrEmpty(savedAvatar.RelativePath))
                {
                    previewUrl = _generateHelper.GeneratePreviewUrl(savedAvatar.RelativePath);
                }

                return BuildCustomerProjection(newUser, repoUser, previewUrl);


            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                {
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                }
                throw;
            }
            finally
            {
                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    await _attachmentService.CleanupTempFiles(dto.temp);
                }
            }
        }


        public async Task<GetCustomerDto> PutCustomer(Guid userId, PutCustomerdto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                //if (!Guid.TryParse(dto.UserId,out Guid userIdGuid))
                //    throw now exception($"Invalid userId format:{dto.UserId}");

                string secureRepoKey = await _helperGet.GetRepoKeyByIdAsync(dto.Repo_Id.Value);
                var repoUser = await _context.RepoUsers
                    .FirstOrDefaultAsync(x => x.UserId == userId
                    && x.RepoKey == secureRepoKey)
                    ?? throw new Exception($"customer '{dto.CustomerName}' not found");

                var loginUser = await _context.LOGIN_MASTER
                   .FirstOrDefaultAsync(x => x.UserID == userId)
                   ?? throw new Exception($"Login not found for '{dto.CustomerName}'");

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                    var permFolder = $"Customer-{userId}";
                    var relativePath = $"{permUserId}/{permFolder}";

                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        "Customer",
                        dto.temp.temps,
                        relativePath,
                        userId.ToString(),
                        "Customer"
                    );
                }

                if (!string.IsNullOrEmpty(dto.MailId)) repoUser.MailId = dto.MailId;
                if (!string.IsNullOrEmpty(dto.PhoneNumber)) repoUser.PhoneNumber = dto.PhoneNumber;

                if (!string.IsNullOrWhiteSpace(dto.CustomerName))
                {
                    repoUser.UserName = dto.CustomerName;
                }
                if (dto.Status.HasValue)
                {
                    string status = dto.Status switch
                    {
                        1 => "Active",
                        17 => "Inactive",
                        _ => throw new Exception("Status must be 1 (Active) or 17 (Inactive)")
                    };

                    repoUser.Status = status;
                    loginUser.Status = status;
                }

                if (attachmentResult?.Attachments?.Any() == true)
                {
                    var oldAttachments = await _context.AttachmentMaster
                        .Where(a => a.ModuleId == userId.ToString()
                                 && a.Module == "Customer"
                                 && a.Status == "Active")
                        .ToListAsync();

                    foreach (var old in oldAttachments)
                    {
                        old.Status = "Inactive";
                        _context.AttachmentMaster.Update(old);
                    }

                    await _context.AttachmentMaster.AddRangeAsync(attachmentResult.Attachments);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var latestAvatar = await _context.AttachmentMaster
                    .Where(a => a.ModuleId == userId.ToString()
                             && a.Module == "Customer"
                             && a.Status == "Active")
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                string? previewUrl = null;
                if (latestAvatar != null && !string.IsNullOrEmpty(latestAvatar.RelativePath))
                {
                    previewUrl = _generateHelper.GeneratePreviewUrl(latestAvatar.RelativePath);
                }

                return BuildCustomerProjection(loginUser, repoUser, previewUrl);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                {
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                }
                throw;
            }
            finally
            {
                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    await _attachmentService.CleanupTempFiles(dto.temp);
                }
            }
        }



        private static GetCustomerDto BuildCustomerProjection(
            LOGIN_MASTER user,
            RepoUserList repoUser,
            string? previewUrl = null,
            string? avatarPath = null,
            GenerateHelper generateHelper = null)
        {
            string? finalPreviewUrl = previewUrl;
            if (!string.IsNullOrEmpty(finalPreviewUrl) && !finalPreviewUrl.StartsWith("http"))
            {
                finalPreviewUrl = generateHelper?.GeneratePreviewUrl(finalPreviewUrl);
            }
            return new GetCustomerDto
            {
                CustomerName = repoUser.UserName,
                UserName = user.UserName,
                MailId = repoUser.MailId,
                PhoneNumber = repoUser.PhoneNumber,
                Repokey = repoUser.RepoKey,
                Status = repoUser.Status,
                WGUserName = user.UserName,
                UserId = user.UserID,
                PreviewUrl = finalPreviewUrl,
                AvatarPath = finalPreviewUrl
            };
        }

        //    public void ProcessCustomerAttachmentJson(GetRepoUserData customerData)
        //    {
        //        if (string.IsNullOrEmpty(customerData?.Attachment_JSON)) return;

        //        try
        //        {
        //            using var doc = JsonDocument.Parse(customerData.Attachment_JSON);
        //            var root = doc.RootElement;
        //            var first = root.EnumerateArray().FirstOrDefault();
        //            if (first.ValueKind == JsonValueKind.Object && 
        //                first.TryGetProperty("relativepath", out var relPathEl) &&
        //                relPathEl.ValueKind == JsonValueKind.String)
        //            {
        //                var relativePath = relPathEl.GetString();
        //                if (!string.IsNullOrEmpty(relativePath))
        //                {
        //                    string fullUrl = _generateHelper.GeneratePreviewUrl(relativePath);
        //                    customerData.PreviewUrl = fullUrl;
        //                    customerData.AvatarPath = fullUrl;
        //                }
        //            }
        //        }
        //        catch
        //        {

        //        }
        //    }
        //}
    }
}
