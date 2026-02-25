using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using static APIGateWay.ModalLayer.Helper.HelperModal;

namespace APIGateWay.DomainLayer.Service
{
    public class RepoService : IRepoService
    {
        private readonly APIGatewayDBContext _context;
        private readonly ILoginContextService _loginContext;
        private readonly ILoginService _loginService;
        private readonly HttpClient _http;
        private readonly APIGateWayCommonService _commonService;
        private readonly IAttachmentService _attachmentService;
        public RepoService(APIGatewayDBContext dBContext, ILoginContextService contextService, ILoginService login, HttpClient http, APIGateWayCommonService commonService, IAttachmentService attachmentService)
        {
            _context = dBContext;
            _loginContext = contextService;
            _loginService = login;
            _http = http;
            _commonService = commonService;
            _attachmentService = attachmentService;
        }

        #region Create Repo user and trigger a repo master insert
        public async Task<GetRepo> PostRepo(PostRepoDto repo)
        {
            using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var userDtos = repo.userLists;

                #region Create LOGIN_MASTER Users

                foreach (var userDto in userDtos)
                {
                    var existingUser =
                        await _context.LOGIN_MASTER
                            .FirstOrDefaultAsync(x =>
                                x.UserName == userDto.UserName);

                    if (existingUser != null)
                    {
                        throw new Exception(
                            $"{userDto.UserName} already exists"
                        );
                    }

                    var (hash, salt) =
                        _loginService.HashPasswordAgron(
                            userDto.Password
                        );

                    var newUser = new LOGIN_MASTER
                    {
                        UserName = userDto.UserName,
                        PasswordHash = hash,
                        Salt = salt,
                        DBName = _loginContext.databaseName,
                        Password = userDto.Password,
                        Status = "Active",
                        Role = userDto.Role,
                        ClientId = null,
                    };

                    _context.LOGIN_MASTER.Add(newUser);
                    await _context.SaveChangesAsync();

                    // Send UserId back to repo mapping
                    userDto.UserId = newUser.UserID;
                }

                #endregion

                #region Create Repository

                repo.CreatedBy = _loginContext.userId;
                var result =
            await InsertOrUpdateRepository(
                repo,
                _loginContext.databaseName
            );

                #endregion

                await transaction.CommitAsync();
                // 🔥 100% Success! Now we can safely delete the temporary files
                if (repo.temp?.temps != null && repo.temp.temps.Any())
                {
                    await _attachmentService.CleanupTempFiles(repo.temp);
                }

                var repoProjection = BuildRepoProjection(result.RepoEntity, result.RepoUsers);
                return repoProjection;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        #endregion

        #region Insert Repo master into table 
        public async Task<RepoInsertResult> InsertOrUpdateRepository(
        PostRepoDto data,
        string DBName
        )
        {
            #region Get Repo Sequence

            string seriesName = "REPO_Sequence";

            var pSeriesName = new SqlParameter("@SeriesName", seriesName);

            var nextSeq = await _commonService
                .ExecuteGetItemAsyc<SequenceResult>(
                    "GetNextNumber",
                    pSeriesName
                );

            var repoKey = $"R{nextSeq[0].CurrentValue}";

            #endregion

            #region Process Attachments (Before creating the Entity)

            string finalDescription = data.Description;
            ProcessedAttachmentResult attachmentResult = null;

            // 2. ONLY run the attachment logic if temps actually exist
            if (data.temp != null && data.temp.temps != null && data.temp.temps.Any())
            {
                var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                var permRepoFolder = $"{repoKey}-{data.Title}";
                var relativePath = $"{permUserId}/{permRepoFolder}";

                // Safely call the service since we know temps is not null
                attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                    data.Description,
                    data.temp.temps,
                    relativePath,
                    repoKey,
                    "RepositoryMaster"
                );

                // 3. Update the description to use the new HTML with permanent URLs
                finalDescription = attachmentResult.UpdatedHtml;
            }
            #endregion

            #region Insert Repo Master

            var newRepo = new PostRepositoryModel
            {
                RepoKey = repoKey,
                SiNo = nextSeq[0].CurrentValue,
                Repo_Id = Guid.NewGuid(),

                Title = data.Title,
                Description = finalDescription,

                CreatedAt = DateTime.UtcNow,
                CreatedBy = data.CreatedBy,

                Status = "Active",

                Owner1 = data.Owner1,
                Owner2 = data.Owner2
            };

            _context.RepositoryMasters.Add(newRepo);
            await _context.SaveChangesAsync();

            #endregion

            #region Insert Repo Users Mapping

            var insertedUsers = new List<RepoUserList>();

            foreach (var mail in data.userLists)
            {
                string usersSeries = "RepositoryUserList";

                var pUserSeries = new SqlParameter("@SeriesName", usersSeries);

                var nextUserSeq = await _commonService
                    .ExecuteGetItemAsyc<SequenceResult>(
                        "GetNextNumber",
                        pUserSeries
                    );

                var userEntity = new RepoUserList
                {
                    SiNo = nextUserSeq[0].CurrentValue,
                    UserName = mail.UserName,
                    MailId = mail.MailId,
                    UserId = mail.UserId,
                    PhoneNumber = mail.PhoneNumber,
                    RepoKey = repoKey,
                    Status = "Active"
                };

                _context.RepoUsers.Add(userEntity);
              
                insertedUsers.Add(userEntity);
            }
            if (attachmentResult != null && attachmentResult.Attachments != null && attachmentResult.Attachments.Any())
            {
                _context.AttachmentMaster.AddRange(attachmentResult.Attachments);
            }

            await _context.SaveChangesAsync();

            #endregion

            return new RepoInsertResult
            {
                RepoEntity = newRepo,
                RepoUsers = insertedUsers
            };
        }
        #endregion

        #region getting Repo value after posting success
        private static GetRepo BuildRepoProjection(PostRepositoryModel repoEntity, List<RepoUserList> repoUsers)
        {
            var repoUserListJson =
                JsonConvert.SerializeObject(
                    repoUsers.Select(x => new RepoUser
                    {
                        UserName = x.UserName,
                        PhoneNumber = x.PhoneNumber,
                        MailId = x.MailId,
                        Status = x.Status
                    }).ToList()
                );

            return new GetRepo
            {
                Repo_Id = repoEntity.Repo_Id,
                RepoKey = repoEntity.RepoKey,
                Title = repoEntity.Title,
                Description = repoEntity.Description,
                CreatedAt = repoEntity.CreatedAt,
                CreatedBy = repoEntity.CreatedBy,
                Status = repoEntity.Status,
                OwnerName = null, // if needed map separately
                RepoUserList = repoUserListJson
            };
        }
        #endregion
    }
}


