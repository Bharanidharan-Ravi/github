using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModelLayer.ErrorException;
using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModalLayer.Helper.HelperModal;
using static APIGateWay.ModelLayer.ErrorException.Exceptionlist;

namespace APIGateWay.DomainLayer.Service
{
    public class RepoService : IRepoService
    {
        private readonly APIGatewayDBContext _context;
        private readonly ILoginContextService _loginContext;
        private readonly ILoginService _loginService;
        private readonly HttpClient _http;
        private readonly APIGateWayCommonService _commonService;

        public RepoService(APIGatewayDBContext dBContext, ILoginContextService contextService, ILoginService login, HttpClient http, APIGateWayCommonService commonService)
        {
            _context = dBContext;
            _loginContext = contextService;
            _loginService = login;
            _http = http;
            _commonService = commonService;
        }

        public async Task<string> PostRepo(PostRepoDto repo)
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

                var repoCreated =
                    await InsertOrUpdateRepository(
                        repo,
                        _loginContext.databaseName
                    );

                #endregion

                await transaction.CommitAsync();

                return repoCreated;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<string> InsertOrUpdateRepository(
    PostRepoDto data,
    string DBName
)
        {
            PostRepositoryModel newRepo = null;

            #region Get Repo Sequence

            string seriesName = "REPO_Sequence";

            var pSeriesName =
                new SqlParameter("@SeriesName", seriesName);

            var nextSeq =
                await _commonService
                    .ExecuteGetItemAsyc<SequenceResult>(
                        "GetNextNumber",
                        pSeriesName
                    );

            var repoKey =
                $"R{nextSeq[0].CurrentValue}";

            #endregion

            #region Insert Repo Master

            newRepo = new PostRepositoryModel
            {
                RepoKey = repoKey,
                SiNo = nextSeq[0].CurrentValue,
                Repo_Id = Guid.NewGuid(),

                Title = data.Title,
                Description = data.Description,

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

            foreach (var mail in data.userLists)
            {
                string usersSeries =
                    "RepositoryUserList";

                var pUserSeries =
                    new SqlParameter(
                        "@SeriesName",
                        usersSeries
                    );

                var nextUserSeq =
                    await _commonService
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
            }

            await _context.SaveChangesAsync();

            #endregion

            #region Return Repo

            Guid? repoId = newRepo.Repo_Id;

            var parameters = new SqlParameter[]
            {
        new SqlParameter("@DatabaseName", DBName),
        new SqlParameter(
            "@RepoId",
            repoId ?? (object)DBNull.Value
        )
            };

            //var repoData =
            //    await _commonService
            //        .ExecuteGetItemAsyc<RepoData>(
            //            "GETALLREPO",
            //            parameters
            //        );
            string message = "Success";
            return message;

            #endregion
        }

        public async Task<PostRepositoryModel> createRepoAsync (PostRepositoryModel repo, string DBname)
        {
            //var response = await _http.PostAsJsonAsync("api/tickets/Repository/PostRepository", repo, DBname);
            var url = $"api/tickets/Repository/PostRepository?DbName={DBname}";
            var response = await _http.PostAsJsonAsync(url, repo);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to create repository: " + response.ReasonPhrase);
            }

            var result = await response.Content.ReadFromJsonAsync<PostRepositoryModel>();

            if (result == null)
                throw new Exception("Invalid API response.");

            return result; 
        }
    }
}


//public async Task<PostRepositoryModel> PostRepo(LoginMasterDto login, ClientMasterDto clientMaster, PostRepositoryModel repo)
//{
//    LOGIN_MASTER newUser = null;
//    ClientMaster client = null;
//    List<CLIENTSMAILIDS> createdMailIds = new();

//    try
//    {
//        // Common: check if username already exists
//        var existingUser = await _dbContext.lOGIN_MASTER
//        .FirstOrDefaultAsync(x => x.UserName == login.UserName);

//        if (existingUser != null)
//            throw new Exceptionlist.UserAlreadyExistsException($"{login.UserName} already exists.");

//        // Hash password
//        var (hash, salt) = _loginService.HashPasswordAgron(login.Password);

//        // ✅ CASE 1: Register for CLIENT
//        if (clientMaster == null)
//            throw new ArgumentException("Client details must be provided when CreatedFor = 'Client'");

//        // Create ClientMaster
//        client = new ClientMaster
//        {
//            Client_Code = clientMaster.ClientCode,
//            Client_Name = clientMaster.ClientName,
//            Description = clientMaster.Description,
//            Created_By = "1",
//            Created_On = DateTime.Now,
//            Valid_From = clientMaster.Valid_From,
//            Status = "Active"
//        };
//        _dbContext.clientMasters.Add(client);
//        await _dbContext.SaveChangesAsync();

//        foreach (var mail in clientMaster.CLIENTSMAILIDS)
//        {
//            var mailEntity = new CLIENTSMAILIDS
//            {
//                Client_Id = client.Client_Id,
//                MailIds = mail.MailIds
//            };

//            createdMailIds.Add(mailEntity);  // store for rollback
//            _dbContext.cLIENTSMAILIDs.Add(mailEntity);
//        }

//        await _dbContext.SaveChangesAsync();

//        // Create LoginMaster for Client
//        newUser = new LOGIN_MASTER
//        {
//            UserName = login.UserName,
//            PasswordHash = hash,
//            Salt = salt,
//            Password = login.Password,
//            DBName = login.DBName,
//            Status = "Active",
//            Role = login.Role,
//            ClientId = client.Client_Id,
//        };

//        _dbContext.lOGIN_MASTER.Add(newUser);
//        await _dbContext.SaveChangesAsync();

//        repo.Client_Id = client.Client_Id;
//        repo.Created_By = _loginContext.userId;
//        var DBname = _loginContext.databaseName;

//        var repoCreated = await createRepoAsync(repo, DBname);

//        return repoCreated;
//    }
//    catch (Exception ex)
//    {
//        if (newUser != null)
//        {
//            _dbContext.lOGIN_MASTER.Remove(newUser);
//        }
//        if (client != null)
//        {
//            _dbContext.clientMasters.Remove(client);
//        }
//        if (createdMailIds.Any())
//            _dbContext.cLIENTSMAILIDs.RemoveRange(createdMailIds);

//        await _dbContext.SaveChangesAsync();
//        throw new ArgumentException("Failed to register a client");
//    }
//}
