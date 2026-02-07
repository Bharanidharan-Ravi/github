using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModelLayer.ErrorException;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static APIGateWay.ModelLayer.ErrorException.Exceptionlist;

namespace APIGateWay.DomainLayer.Service
{
    public class RepoService : IRepoService
    {
        private readonly APIGatewayDBContext _dbContext;
        private readonly ILoginContextService _loginContext;
        private readonly ILoginService _loginService;
        private readonly HttpClient _http;

        public RepoService(APIGatewayDBContext dBContext, ILoginContextService contextService, ILoginService login, HttpClient http)
        {
            _dbContext = dBContext;
            _loginContext = contextService;
            _loginService = login;
            _http = http;
        }

        public async Task<PostRepositoryModel> PostRepo(LoginMasterDto login, ClientMasterDto clientMaster, PostRepositoryModel repo)
        {
            LOGIN_MASTER newUser = null;
            ClientMaster client = null;
            List<CLIENTSMAILIDS> createdMailIds = new();

            try
            {
                // Common: check if username already exists
                var existingUser = await _dbContext.lOGIN_MASTER
                .FirstOrDefaultAsync(x => x.UserName == login.UserName);

                if (existingUser != null)
                    throw new Exceptionlist.UserAlreadyExistsException($"{login.UserName} already exists.");

                // Hash password
                var (hash, salt) = _loginService.HashPasswordAgron(login.Password);

                // ✅ CASE 1: Register for CLIENT
                if (clientMaster == null)
                    throw new ArgumentException("Client details must be provided when CreatedFor = 'Client'");

                // Create ClientMaster
                client = new ClientMaster
                {
                    Client_Code = clientMaster.ClientCode,
                    Client_Name = clientMaster.ClientName,
                    Description = clientMaster.Description,
                    Created_By = "1",
                    Created_On = DateTime.Now,
                    Valid_From = clientMaster.Valid_From,
                    Status = "Active"
                };
                _dbContext.clientMasters.Add(client);
                await _dbContext.SaveChangesAsync();

                foreach (var mail in clientMaster.CLIENTSMAILIDS)
                {
                    var mailEntity = new CLIENTSMAILIDS
                    {
                        Client_Id = client.Client_Id,
                        MailIds = mail.MailIds
                    };

                    createdMailIds.Add(mailEntity);  // store for rollback
                    _dbContext.cLIENTSMAILIDs.Add(mailEntity);
                }

                await _dbContext.SaveChangesAsync();

                // Create LoginMaster for Client
                newUser = new LOGIN_MASTER
                {
                    UserName = login.UserName,
                    PasswordHash = hash,
                    Salt = salt,
                    Password = login.Password,
                    DBName = login.DBName,
                    Status = "Active",
                    Role = login.Role,
                    ClientId = client.Client_Id,
                };

                _dbContext.lOGIN_MASTER.Add(newUser);
                await _dbContext.SaveChangesAsync();

                repo.Client_Id = client.Client_Id;
                repo.Created_By = _loginContext.userId;
                var DBname = _loginContext.databaseName;

                var repoCreated = await createRepoAsync(repo, DBname);

                return repoCreated;
            }
            catch (Exception ex)
            {
                if (newUser != null)
                {
                    _dbContext.lOGIN_MASTER.Remove(newUser);
                }
                if (client != null)
                {
                    _dbContext.clientMasters.Remove(client);
                }
                if (createdMailIds.Any())
                    _dbContext.cLIENTSMAILIDs.RemoveRange(createdMailIds);

                await _dbContext.SaveChangesAsync();
                throw new ArgumentException("Failed to register a client");
            }
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
