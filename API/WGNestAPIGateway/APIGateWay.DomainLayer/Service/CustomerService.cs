using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
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

        public CustomerService(APIGatewayDBContext dBContext, ILoginContextService contextService, ILoginService login, APIGateWayCommonService commonService, IHelperGetData helperGet)
        {
            _context = dBContext;
            _loginContext = contextService;
            _loginService = login;
            _commonService = commonService;
            _helperGet = helperGet;

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
    

public async Task<GetCustomerDto> PostCustomer(PostCustomerDto dto,string dbName)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

                string secureRepoKey = await _helperGet.GetRepoKeyByIdAsync(dto.Repo_Id.Value);
                var existing = await _context.LOGIN_MASTER.FirstOrDefaultAsync(x=>x.UserName == dto.UserName);


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

                        var (hash, salt) =
                            _loginService.HashPasswordAgron(
                                dto.Password
                            );

                        var newUser = new LOGIN_MASTER
                        {
                            UserName = dto.UserName,
                            PasswordHash = hash,
                            Salt = salt,
                            DBName =dbName,
                            Password = dto.Password,
                            Status = "Active",
                            Role = dto.Role,
                            ClientId = null,
                        };

                        _context.LOGIN_MASTER.Add(newUser);
                        await _context.SaveChangesAsync();
                string usersSeries = "RepostoryUserlist";
                var pUserSeries=new SqlParameter("@SeriesName",usersSeries);
                var nextUserSeq = await _commonService
                    .ExecuteGetItemAsyc<SequenceResult> (
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

                await transaction.CommitAsync();
                return BuildCustomerProjection(newUser, repoUser);

               
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<GetCustomerDto> PutCustomer(Guid userId, PutCustomerdto dto,string dbName)
        {
            using var transaction=await _context.Database.BeginTransactionAsync();
            try
            {
                //if (!Guid.TryParse(dto.UserId,out Guid userIdGuid))
                //    throw now exception($"Invalid userId format:{dto.UserId}");

                string secureRepoKey = await _helperGet.GetRepoKeyByIdAsync(dto.Repo_Id.Value);
                var repoUser = await _context.RepoUsers
                    .FirstOrDefaultAsync(x => x.UserId == userId 
                    && x.RepoKey == secureRepoKey)
                    ?? throw new Exception($"customer '{dto.CustomerName}' not found");

              

                //if (repoUser.UserId == null || repoUser.UserId == Guid.Empty)
                //    throw new Exception($"Login not found for '{dto.CustomerName}'");
                //Guid UserIdValue = repoUser.UserId.Value;

                var loginUser = await _context.LOGIN_MASTER
                   .FirstOrDefaultAsync(x => x.UserID == userId)
                   ?? throw new Exception($"Login not found for '{dto.CustomerName}'");

                if (!string.IsNullOrEmpty(dto.MailId)) repoUser.MailId = dto.MailId;
                if (!string.IsNullOrEmpty(dto.PhoneNumber)) repoUser.PhoneNumber = dto.PhoneNumber;
                //if (!string.IsNullOrEmpty(dto.Status)) repoUser.Status = dto.Status;
                //if (!string.IsNullOrEmpty(dto.Status)) loginUser.Status = dto.Status;
                if (!string.IsNullOrEmpty(dto.NewCustomerName)) repoUser.UserName = dto.NewCustomerName;
                if (!string.IsNullOrEmpty(dto.Status))
                {
                    if (dto.Status != "Active" && dto.Status != "Inactive")
                        throw new Exception("Status must be 'Actice' or 'Inactive'");
                    repoUser.Status = dto.Status;  
                    loginUser.Status=dto.Status;
                }


                    await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return BuildCustomerProjection(loginUser, repoUser);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }



        private static GetCustomerDto BuildCustomerProjection(
            LOGIN_MASTER user,RepoUserList repoUser)
        {
            return new GetCustomerDto
            {
                CustomerName = repoUser.UserName,
                UserName = user.UserName,
                MailId = repoUser.MailId,
                PhoneNumber = repoUser.PhoneNumber,
                Repokey = repoUser.RepoKey,
                Status = repoUser.Status,
                WGUserName = user.UserName

            };
        }
    }
}
