using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.EntityFrameworkCore;
using Konscious.Security.Cryptography;

namespace APIGateWay.DomainLayer.Service
{
    public class LoginService : ILoginService
    {
        private readonly APIGatewayDBContext _context;
        private readonly APIGateWayCommonService _commonService;
        private readonly IConfiguration _configuration;
        private readonly ILoginContextService _loginContext;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int DegreeofParallelism = 8;
        private const int Iterations = 4;
        private const int MemorySize = 1024 * 64;

        public LoginService(APIGatewayDBContext context, APIGateWayCommonService commonService, IConfiguration configuration, ILoginContextService loginContextService)
        {
            _context = context;
            _commonService = commonService;
            _configuration = configuration;
            _loginContext = loginContextService;
            /*_connectionHelper = new ConnectionHelper(configuration);*/
        }

        #region login function
        public async Task<List<GetUserforValidate>> GetUser(string username, string password, string deviceInfo)
        {
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@username", username),
                new SqlParameter("@password", password)
            };


            var userList = await _commonService.ExecuteGetItemAsyc<GetUserforValidate>("VALIDATEUSER", parameters);
            if (userList == null || userList.Count == 0)
            {
                throw new Exceptionlist.LoginException("No Valid User.", username, deviceInfo, password);
            }

            var user = userList.FirstOrDefault();

            if (user.Status != "
                ")
            {
                throw new Exceptionlist.LoginException("Your account is inactive. Please contact Admin.", username, deviceInfo, password);
            }

            bool verifyuser = VerifyPassword(password, userList[0].PasswordHash, userList[0].Salt);

            if (!verifyuser)
            {
                throw new Exceptionlist.LoginException("Invalid username or password.", username, deviceInfo, password);
            }

            await SaveUserSession(user.UserId, user.UserName, user.DBName, deviceInfo, DateTime.Now, "0");

            return userList;
        }

        public static bool VerifyPassword(string Password, string StoredHash, string StoredSalt)
        {
            byte[] salt = Convert.FromBase64String(StoredSalt);

            var hashString = new Argon2id(Encoding.UTF8.GetBytes(Password))
            {
                Salt = salt,
                DegreeOfParallelism = DegreeofParallelism,
                MemorySize = MemorySize,
                Iterations = Iterations,
            };
            byte[] hashBytes = hashString.GetBytes(HashSize);
            string hashedPassword = Convert.ToBase64String(hashBytes);
            //string saltBase = Convert.ToBase64String(salt);
            return hashedPassword == StoredHash;
        }
        #endregion

        #region save the login data
        private async Task SaveUserSession(Guid userId, string userName, string databaseName, string deviceInfo, DateTime loginTimestamp, string autoLogout)
        {
            var parameters = new[]
            {
                new SqlParameter("@userid", userId.ToString()),
                new SqlParameter("@username", userName),
                new SqlParameter("@database", "databaseName"),
                new SqlParameter("@device", deviceInfo),
                new SqlParameter("@login", loginTimestamp),
                new SqlParameter("@logout", DBNull.Value)
            };

            // Execute the stored procedure INSERTUSERLOG
            await _commonService.ExecuteNonModalAsync("INSERTUSERLOG", parameters);
        }
        #endregion

        #region User Login register 
        public async Task<GetUserList> RegisterUserAsync(RegisterRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate CreatedFor
                if (string.IsNullOrEmpty(request.CreatedFor))
                    throw new ArgumentException("CreatedFor is required (must be 'Client' or 'Employee').");

                // Common: check if username already exists
                var existingUser = await _context.LOGIN_MASTER
                    .FirstOrDefaultAsync(x => x.UserName == request.Login.UserName);

                if (existingUser != null)
                    throw new Exceptionlist.UserAlreadyExistsException($"{request.Login.UserName} already exists.");

                // Hash password
                var (hash, salt) = HashPasswordAgron(request.Login.Password);

                LOGIN_MASTER newUser = null;
                ClientMaster client = null;

                // ✅ CASE 1: Register for CLIENT
                if (request.CreatedFor.Equals("Client", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Client == null)
                        throw new ArgumentException("Client details must be provided when CreatedFor = 'Client'");

                    // Create ClientMaster
                    client = new ClientMaster
                    {
                        Client_Code = request.Client.ClientCode,
                        Client_Name = request.Client.ClientName,
                        Description = request.Client.Description,
                        Created_By = "1",
                        Created_On = DateTime.Now,
                        Valid_From = request.Client.Valid_From,
                        Status = "Active"
                    };
                    _context.clientMasters.Add(client);
                    await _context.SaveChangesAsync();

                    // Create LoginMaster for Client
                    newUser = new LOGIN_MASTER
                    {
                        UserName = request.Login.UserName,
                        PasswordHash = hash,
                        Salt = salt,
                        Password = request.Login.Password,
                        DBName = request.Login.DBName,
                        Status = "Active",
                        Role = request.Login.Role,
                        ClientId = client.Client_Id,
                    };

                    _context.LOGIN_MASTER.Add(newUser);
                    await _context.SaveChangesAsync();
                }

                // ✅ CASE 2: Register for EMPLOYEE
                else if (request.CreatedFor.Equals("Employee", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Employee == null)
                        throw new ArgumentException("Employee details must be provided when CreatedFor = 'Employee'");

                    // Create LoginMaster first (for employee login)
                    newUser = new LOGIN_MASTER
                    {
                        UserName = request.Login.UserName,
                        PasswordHash = hash,
                        Salt = salt,
                        ClientId = null,
                        Password = request.Login.Password,
                        DBName = request.Login.DBName,
                        Status = "Active",
                        Role = request.Login.Role
                    };

                    _context.LOGIN_MASTER.Add(newUser);
                    await _context.SaveChangesAsync();

                    // Create EmployeeMaster linked to login
                    var employee = new EMPLOYEEMASTER
                    {
                        EmployeeName = request.Employee.EmployeeName,
                        Team = request.Employee.Team,
                        Role = request.Employee.Role,
                        Specialization = request.Employee.Specialization,
                        Email = request.Employee.Email,
                        PhoneNumber = request.Employee.PhoneNumber,
                        CreatedBy = "System",
                        EmployeeID = newUser.UserID,
                        Status = "Active"
                    };

                    _context.eMPLOYEEMASTERs.Add(employee);
                    await _context.SaveChangesAsync();
                }
                else if (request.CreatedFor.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Employee == null)
                        throw new ArgumentException("Employee details must be provided when CreatedFor = 'Employee'");

                    // Create LoginMaster first (for employee login)
                    newUser = new LOGIN_MASTER
                    {
                        UserName = request.Login.UserName,
                        PasswordHash = hash,
                        Salt = salt,
                        ClientId = null,
                        Password = request.Login.Password,
                        DBName = request.Login.DBName,
                        Status = "Active",
                        Role = request.Login.Role
                    };

                    _context.LOGIN_MASTER.Add(newUser);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    throw new ArgumentException("Invalid CreatedFor value. Must be 'Client' or 'Employee'.");
                }

                // ✅ Commit transaction
                await transaction.CommitAsync();

                return new GetUserList
                {
                    UserID = newUser.UserID,
                    Username = newUser.UserName,
                    Status = newUser.Status
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public (string hash, string salt) HashPasswordAgron(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            var hashString = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = DegreeofParallelism,
                MemorySize = MemorySize,
                Iterations = Iterations,
            };
            byte[] hashBytes = hashString.GetBytes(HashSize);
            string hashedPassword = Convert.ToBase64String(hashBytes);
            string saltBase = Convert.ToBase64String(salt);
            return (hashedPassword, saltBase);
        }

        #endregion

        //public async Task<List<GetEmployee>> GetEmployeeMaster()
        //{
        //    var parameters = new SqlParameter[]
        //   {
        //        new SqlParameter("@DatabaseName", _loginContext.databaseName),
        //   };
        //    var response = await _commonService.ExecuteGetItemAsyc<GetEmployee>("GETEMPLOYEEMASTER", parameters);
        //    return response;
        //}
    }
}
