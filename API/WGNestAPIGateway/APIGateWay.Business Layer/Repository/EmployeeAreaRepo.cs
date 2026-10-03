using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.Session;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class EmployeeAreaRepo : IEmployeeAreaRepo
    {
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly IRequestStepContext _stepContext;
        private readonly APIGatewayDBContext _db;
        private readonly IAttachmentService _attachmentService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly INotificationRepository _notificationRepository;

        public EmployeeAreaRepo(IDomainService domainService, ILoginContextService loginContext, IRequestStepContext stepContext, APIGatewayDBContext db, IAttachmentService attachmentService, IRealtimeNotifier realtimeNotifier, INotificationRepository notificationRepository)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _stepContext = stepContext;
            _db = db;
            _attachmentService = attachmentService;
            _realtimeNotifier = realtimeNotifier;
            _notificationRepository = notificationRepository;
        }

        public async Task<GetHoliday> CreateHolidayAsync(PostHolidayDto dto)
        {
            return await _domainService.ExecuteInTransactionAsync(async () =>

            {
                var timer = _stepContext.StartStep();
                try
                {
                    var entity = new HolidayMaster
                    {
                        HolidayDate = dto.HolidayDate,
                        Day = dto.HolidayDate.DayOfWeek.ToString(),
                        HolidayName = dto.HolidayName,
                        CreatedAt = DateTime.Now,
                        UpdatedBy = _loginContext.userId

                    };
                    await _db.Set<HolidayMaster>().AddAsync(entity);
                    await _db.SaveChangesAsync();

                    _stepContext.Success("HolidayMaster", "INSERT", entity.Id.ToString(), timer);
                    return new GetHoliday
                    {
                        Id = entity.Id,
                        HolidayDate = entity.HolidayDate,
                        Day = entity.Day,
                        HolidayName = entity.HolidayName,
                        CreatedAt = entity.CreatedAt,
                        UpdatedBy = entity.UpdatedBy
                    };
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("HolidayMaster", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }

        public async Task<GetHoliday> UpdateHolidayAsync(int id, PostHolidayDto dto)
        {
            return await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    var entity = await _db.Set<HolidayMaster>().FindAsync(id);
                    if (entity == null) throw new Exception("Holiday not found.");

                    entity.HolidayDate = dto.HolidayDate;
                    entity.Day = dto.HolidayDate.DayOfWeek.ToString();
                    entity.HolidayName = dto.HolidayName;
                    entity.UpdatedBy = _loginContext.userId;

                    await _db.SaveChangesAsync();
                    _stepContext.Success("HolidayMaster", "UPDATE", entity.Id.ToString(), timer);
                    return new GetHoliday
                    {
                        Id = entity.Id,
                        HolidayDate = entity.HolidayDate,
                        Day = entity.Day,
                        HolidayName = entity.HolidayName,
                        CreatedAt = entity.CreatedAt,
                        UpdatedBy = entity.UpdatedBy
                    };
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("HolidayMaster", "UPDATE", ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            });
        }
        public async Task<PolicyDto> UploadPolicyAsync(PostPolicyDto dto)
        {
            if (dto.temp?.temps == null || !dto.temp.temps.Any())
                throw new ArgumentException("No files were provided for processing.");

            ProcessedAttachmentResult attachmentResult = null;

            try
            {
                var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                var relativePath = $"{permUserId}/CompanyPolicy";
                var newModuleId = Guid.NewGuid().ToString();

                attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                    "CompanyPolicy",
                    dto.temp.temps,
                    relativePath,
                    newModuleId,
                    "CompanyPolicy"
                );

                var transactionResult = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var timer = _stepContext.StartStep();
                    try
                    {
                        var oldPolicies = await _db.AttachmentMaster
                            .Where(a => a.Module == "CompanyPolicy" && a.Status == "Active")
                            .ToListAsync();

                        foreach (var old in oldPolicies)
                        {
                            old.Status = "Inactive";
                            _db.AttachmentMaster.Update(old);
                        }

                        await _db.AttachmentMaster.AddRangeAsync(attachmentResult.Attachments);
                        await _db.SaveChangesAsync();

                        var newAttachment = attachmentResult.Attachments.First();
                        _stepContext.Success("AttachmentMaster", "INSERT", newAttachment.AttachmentId.ToString(), timer);

                        return new PolicyDto
                        {
                            Id = newAttachment.AttachmentId,
                            FileName = newAttachment.FileName,
                            FileUrl = newAttachment.RelativePath,
                            UpdatedAt = newAttachment.CreatedAt ?? DateTime.UtcNow,
                            UpdatedBy = _loginContext.userName
                        };
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("AttachmentMaster", "INSERT", ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                });

                try
                {
                    var actorId = _loginContext.userId;
                    var actorName = _loginContext.userName;
                    var moduleGuid = Guid.NewGuid();

                    var allEmployeeIds = await _db.eMPLOYEEMASTERs
                        .Where(e => e.EmployeeID != actorId)
                        .Select(e => e.EmployeeID)
                        .ToListAsync();

                    var audiences = allEmployeeIds.Select(empId => new NotificationAudience
                    {
                        AudienceType = "USER",
                        AudienceValue = empId.ToString()
                    }).ToList();

                    if (audiences.Any())
                    {

                        var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
                        {
                            EventType = "POLICY_UPDATED",
                            EntityType = "COMPANY_POLICY",
                            EntityId = moduleGuid.ToString(),
                            Title = "Company Policy Updated",
                            Message = $"{actorName} has uploaded a new company policy document",
                            ActorId = actorId,
                            ActorName = actorName,
                            Audiences = audiences
                        });

                        foreach (var userId in allEmployeeIds)
                        {
                            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                            {
                                Entity = "Notification",
                                Action = "Created",
                                Payload = new
                                {
                                    NotificationId = notificationId,
                                    CreatedByUserId = actorId,
                                    Module = "CompanyPolicy"
                                },
                                KeyField = "NotificationId",
                                TargetUserId = userId,
                                Timestamp = DateTime.UtcNow,
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n === Notification failed");
                    Console.WriteLine($"Message: {ex.Message}");
                    Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    Console.WriteLine($"=================\n");
                }
                return transactionResult;
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

                throw new Exception("Policy upload failed. Detail: " + (ex.InnerException?.Message ?? ex.Message), ex);
            }
            finally
            {
                if (dto.temp?.temps != null && dto.temp.temps.Any())
                    await _attachmentService.CleanupTempFiles(dto.temp);
            }
        }
    }
}
