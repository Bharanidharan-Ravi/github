using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using APIGateWay.ModelLayer.ErrorException;
using AutoMapper;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class EmployeeRepo : IEmployeeRepo
    {
        private readonly IAttachmentService _attachmentService;
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContextService;
        private readonly IMapper _mapper;
        private readonly APIGatewayDBContext _dbContext;

        public EmployeeRepo(
            IAttachmentService attachmentService,
            IDomainService domainService,
            ILoginContextService loginContext,
            IMapper mapper,
            APIGatewayDBContext Context)
        {
            _attachmentService = attachmentService;
            _domainService = domainService;
            _loginContextService = loginContext;
            _mapper = mapper;
            _dbContext = Context;
        }
        public async Task<GetEmployee> UpdateEmployeeAsync(Guid employeeId, RegisterRequestDto dto)
        {
            // Safety check to ensure the payload actually contains Employee data
            if (dto.Employee == null)
            {
                throw new ArgumentException("Employee details must be provided for the update.");
            }

            ProcessedAttachmentResult attachmentResult = null;
            GetEmployee finalData = null;

            try
            {
                // 1. Validate Username (Fail-fast before processing attachments)
                if (dto.Login != null && !string.IsNullOrEmpty(dto.Login.UserName))
                {
                    // Fetch ONLY the current username as a string (highly optimized query)
                    var currentUserName = await _dbContext.LOGIN_MASTER
                        .Where(x => x.UserID == employeeId)
                        .Select(x => x.UserName)
                        .FirstOrDefaultAsync();

                    // If the user typed a username that is DIFFERENT from their current one
                    if (currentUserName != dto.Login.UserName)
                    {
                        // Check if the new name is already taken by someone else
                        bool isTaken = await _dbContext.LOGIN_MASTER.AnyAsync(x => x.UserName == dto.Login.UserName);

                        if (isTaken)
                        {
                            throw new Exceptionlist.UserAlreadyExistsException($"{dto.Login.UserName} already exists.");
                        }
                    }
                }

                // 2. Process New Attachments (Before DB Transaction)
                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    var permUserId = $"{_loginContextService.userId}-{_loginContextService.userName}";
                    var permFolder = $"Employee-{employeeId}";
                    var relativePath = $"{permUserId}/{permFolder}";

                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        "EmployeeAvatar",
                        dto.temp.temps,
                        relativePath,
                        employeeId.ToString(),
                        "Employee"
                    );
                }

                finalData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // 3. Update LOGIN_MASTER (Username & Future Password)
                    var loginData = await _dbContext.LOGIN_MASTER.FirstOrDefaultAsync(x => x.UserID == employeeId);

                    if (loginData != null && dto.Login != null)
                    {
                        // Update Username
                        if (!string.IsNullOrEmpty(dto.Login.UserName))
                        {
                            loginData.UserName = dto.Login.UserName;
                        }
                        if (string.Equals(dto.Login.Status, "InActive", StringComparison.OrdinalIgnoreCase))
                        {
                            loginData.Status = "Inactive";
                        }
                        if (string.Equals(dto.Login.Status, "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            loginData.Status = "Active";
                        }
                            // Update Password (For future use)
                        if (!string.IsNullOrEmpty(dto.Login.Password))
                        {
                            // var (hash, salt) = HashPasswordAgron(dto.Login.Password);
                            // loginData.PasswordHash = hash;
                            // loginData.Salt = salt;
                        }

                        _dbContext.LOGIN_MASTER.Update(loginData);
                    }

                    // 4. Deactivate Old Attachments (If a new one is uploaded)
                    if (attachmentResult != null && attachmentResult.Attachments.Any())
                    {
                        var oldAttachments = await _dbContext.AttachmentMaster
                            .Where(a => a.ModuleId == employeeId.ToString() &&
                                        a.Module == "EmployeeAvatar" &&
                                        a.Status == "Active")
                            .ToListAsync();

                        foreach (var oldAttach in oldAttachments)
                        {
                            oldAttach.Status = "Inactive";
                            _dbContext.AttachmentMaster.Update(oldAttach);
                        }
                    }

                    // 5. Update EMPLOYEEMASTER
                    var updatedEmployee = await _domainService.UpdateEntityByPredicateWithAttachmentsAsync<EMPLOYEEMASTER>(
                        x => x.EmployeeID == employeeId, // 👈 Safe query!
                        entity =>
                        {
                            entity.EmployeeName = dto.Employee.EmployeeName;
                            entity.Status = dto.Login.Status;
                            entity.Team = dto.Employee.Team;
                            entity.Role = dto.Employee.Role;
                            entity.Specialization = dto.Employee.Specialization;
                            entity.Email = dto.Employee.Email;
                            entity.PhoneNumber = dto.Employee.PhoneNumber;
                            entity.DoB = dto.Employee.DoB;
                        },
                        attachmentResult?.Attachments
                    );

                    // Ensure manual context updates (Login/Attachments) are committed alongside the domain service
                    await _dbContext.SaveChangesAsync();

                    // 6. Fetch the newly active avatar for the response
                    var avatar = await _dbContext.AttachmentMaster
                        .Where(a => a.ModuleId == employeeId.ToString() &&
                                    a.Module == "EmployeeAvatar" &&
                                    a.Status == "Active")
                        .OrderByDescending(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                    return new GetEmployee
                    {
                        UserID = updatedEmployee.EmployeeID,
                        UserName = loginData?.UserName ?? updatedEmployee.EmployeeName,
                        Attachment_JSON = avatar?.RelativePath,
                        Status = updatedEmployee.Status,
                        Team = updatedEmployee.Team,
                        Role = updatedEmployee.Role,
                        Specialization = updatedEmployee.Specialization,
                        Email = updatedEmployee.Email,
                        PhoneNumber = updatedEmployee.PhoneNumber,
                        DoB = updatedEmployee.DoB,
                    };
                });
            }
            catch (Exception ex)
            {
                // Rollback physical files if the DB transaction fails
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

                // Extract the real database error if it exists
                string actualError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                // Throwing the detailed error makes it visible in your API response or logs
                throw new Exceptionlist.InvalidDataException($"Employee update failed. Detail: {actualError}", ex);
            }

            // Cleanup temp files on absolute success
            if (dto.temp?.temps != null && dto.temp.temps.Any())
            {
                await _attachmentService.CleanupTempFiles(dto.temp);
            }

            return finalData;
        }
    }
}
