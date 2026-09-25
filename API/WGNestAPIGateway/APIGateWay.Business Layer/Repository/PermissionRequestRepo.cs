using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.Session;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using APIGateWay.ModelLayer.ErrorException;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class PermissionRequestRepo : IPermissionRequestRepo
    {
        private readonly IDomainService _domainService;
        private readonly APIGatewayDBContext _dBContext;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly INotificationRepository _notificationRepository;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly IRequestStepContext _stepContext;

        private static readonly TimeZoneInfo IndiaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

        public PermissionRequestRepo(
            IDomainService domainService,
            APIGatewayDBContext dbContext,
            IMapper mapper,
            ILoginContextService loginContext,
            INotificationRepository notificationRepository,
            IRealtimeNotifier realtimeNotifier,
            IRequestStepContext stepContext)
        {
            _domainService = domainService;
            _dBContext = dbContext;
            _mapper = mapper;
            _loginContext = loginContext;
            _notificationRepository = notificationRepository;
            _realtimeNotifier = realtimeNotifier;
            _stepContext = stepContext;
        }

        public async Task<GetPermissionRequest> CreatePermissionRequestAsync(PostPermissionRequestDto dto)
        {
            if (!AppRoles.LeaveRequestCreate.Contains(_loginContext.role))
                throw new Exceptionlist.UnauthorizedException("Only employees and admins can submit permission requests.");

            GetPermissionRequest finalData = null;
            PermissionRequestMaster entity = null;

            try
            {
                finalData = await _domainService.ExecuteInTransactionAsync<GetPermissionRequest>(async () =>
                {
                    entity = _mapper.Map<PermissionRequestMaster>(dto);
                    entity.ID = Guid.NewGuid();
                    entity.EMPLOYEE_ID = _loginContext.userId;
                    entity.STATUS = "REQUESTED";
                    entity.REQUESTED_DATE =
                        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);

                    await _dBContext.Set<PermissionRequestMaster>().AddAsync(entity);

                    var timer = _stepContext.StartStep();
                    try
                    {
                        await _dBContext.SaveChangesAsync();
                        _stepContext.Success("PermissionRequest", "INSERT", entity.ID.ToString(), timer);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("PermissionRequest", "INSERT",
                            ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }

                    return _mapper.Map<GetPermissionRequest>(entity);
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Permission request creation failed. Everything was rolled back safely. {ex}", ex);
            }

            if (entity != null)
            {
                await NotifyAdminsAsync(entity);
            }

            return finalData;
        }

        public async Task<GetPermissionRequest> UpdateStatusAsync(Guid id, PostPermissionRequestStatusDto dto)
        {
            if (_loginContext.role != 1)
                throw new Exceptionlist.UnauthorizedException("Only admins can approve or reject permission requests.");

            var entity = await _dBContext.Set<PermissionRequestMaster>().FindAsync(id)
                ?? throw new Exceptionlist.DataNotFoundException($"Permission request '{id}' not found.");

            if (entity.STATUS != "REQUESTED")
                throw new Exceptionlist.InvalidDataException("This permission request has already been decided.");

            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);
                    entity.STATUS = dto.Status;
                    if (dto.Status == "APPROVED")
                    {
                        entity.APPROVED_BY = _loginContext.userId;
                        entity.APPROVED_DATE = now;
                    }
                    else
                    {
                        entity.REJECTED_BY = _loginContext.userId;
                        entity.REJECTED_DATE = now;
                        entity.REJECT_REASON = dto.RejectReason;
                    }

                    await _dBContext.SaveChangesAsync();
                    return true;
                });

                _stepContext.Success("PermissionRequest", "UPDATE", id.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("PermissionRequest", "UPDATE",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

            await NotifyRequesterAsync(entity, dto.Status);
            // Refresh the requester's list and every other admin's list.
            await BroadcastPermissionRequestChangeAsync(entity, "Updated", entity.EMPLOYEE_ID);

            return _mapper.Map<GetPermissionRequest>(entity);
        }

        public async Task<GetPermissionRequest> UpdateActualDurationAsync(Guid id, PostPermissionActualDurationDto dto)
        {
            if (_loginContext.role != 1)
                throw new Exceptionlist.UnauthorizedException("Only admins can record the actual duration taken.");

            var entity = await _dBContext.Set<PermissionRequestMaster>().FindAsync(id)
                ?? throw new Exceptionlist.DataNotFoundException($"Permission request '{id}' not found.");

            if (entity.STATUS != "APPROVED")
                throw new Exceptionlist.InvalidDataException("Only an approved permission request can have its actual duration recorded.");
            if (dto.ActualDurationMinutes <= 0)
                throw new Exceptionlist.InvalidDataException("Actual duration must be greater than zero.");

            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    entity.ACTUAL_DURATION_MINUTES = dto.ActualDurationMinutes;
                    entity.ACTUAL_DURATION_BY = _loginContext.userId;
                    entity.ACTUAL_DURATION_DATE = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);

                    await _dBContext.SaveChangesAsync();
                    return true;
                });

                _stepContext.Success("PermissionRequest", "UPDATE", id.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("PermissionRequest", "UPDATE",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

            await NotifyActualDurationAsync(entity);
            await BroadcastPermissionRequestChangeAsync(entity, "Updated", entity.EMPLOYEE_ID);

            return _mapper.Map<GetPermissionRequest>(entity);
        }

        private async Task NotifyActualDurationAsync(PermissionRequestMaster entity)
        {
            var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
            {
                EventType = "PERMISSION_REQUEST_ACTUAL_DURATION_UPDATED",
                EntityType = "PERMISSION_REQUEST",
                EntityId = entity.ID.ToString(),
                Title = "Permission Duration Updated",
                Message = $"Your permission request ({entity.PERMISSION_DATE:dd-MMM-yyyy}) was recorded as {entity.ACTUAL_DURATION_MINUTES} min taken (of {entity.DURATION_MINUTES} min requested).",
                ActorId = _loginContext.userId,
                ActorName = _loginContext.userName,
                Audiences = new List<NotificationAudience>
                {
                    new NotificationAudience { AudienceType = "USER", AudienceValue = entity.EMPLOYEE_ID.ToString() },
                },
            });

            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
            {
                Entity = "Notification",
                Action = "Created",
                Payload = new { NotificationId = notificationId, CreatedByUserId = _loginContext.userId },
                KeyField = "NotificationId",
                TargetUserId = entity.EMPLOYEE_ID,
                Timestamp = DateTime.UtcNow,
            });
        }

        private Task BroadcastPermissionRequestChangeAsync(PermissionRequestMaster entity, string action, Guid? targetUserId)
        {
            return _realtimeNotifier.BroadcastAsync(new RealtimeMessage
            {
                Entity = "PermissionRequest",
                Action = action,
                Payload = new { ID = entity.ID, EMPLOYEE_ID = entity.EMPLOYEE_ID, STATUS = entity.STATUS },
                KeyField = "ID",
                TargetUserId = targetUserId,
                Timestamp = DateTime.UtcNow,
            });
        }

        private async Task NotifyRequesterAsync(PermissionRequestMaster entity, string status)
        {
            var approved = status == "APPROVED";
            var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
            {
                EventType = approved ? "PERMISSION_REQUEST_APPROVED" : "PERMISSION_REQUEST_REJECTED",
                EntityType = "PERMISSION_REQUEST",
                EntityId = entity.ID.ToString(),
                Title = approved ? "Permission Request Approved" : "Permission Request Rejected",
                Message = approved
                    ? $"Your permission request ({entity.PERMISSION_DATE:dd-MMM-yyyy}, {entity.DURATION_MINUTES} min) was approved."
                    : $"Your permission request ({entity.PERMISSION_DATE:dd-MMM-yyyy}, {entity.DURATION_MINUTES} min) was rejected. Reason: {entity.REJECT_REASON}",
                ActorId = _loginContext.userId,
                ActorName = _loginContext.userName,
                Audiences = new List<NotificationAudience>
                {
                    new NotificationAudience { AudienceType = "USER", AudienceValue = entity.EMPLOYEE_ID.ToString() },
                },
            });

            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
            {
                Entity = "Notification",
                Action = "Created",
                Payload = new { NotificationId = notificationId, CreatedByUserId = _loginContext.userId },
                KeyField = "NotificationId",
                TargetUserId = entity.EMPLOYEE_ID,
                Timestamp = DateTime.UtcNow,
            });
        }

        private async Task NotifyAdminsAsync(PermissionRequestMaster entity)
        {
            // Data ping first: RealtimeNotifier sends non-"Notification" entities to the
            // "global-admin" SignalR group (joined from the JWT role), so every admin's
            // Permission Requests list refreshes even if the lookup below finds nobody.
            await BroadcastPermissionRequestChangeAsync(entity, "Created", null);

            // Admin = login role (LOGIN_MASTER / WGUserDetails), the same role the JWT,
            // the frontend's isAdmin and UpdateStatusAsync use. EMPLOYEEMASTER.Role is a
            // separate column and doesn't reliably mark admins.
            var adminIds = await _domainService.Query<LOGIN_MASTER>()
                .Where(u => u.Role == 1
                         && (u.Status == null || u.Status == "Active")
                         && u.UserID != _loginContext.userId)
                .Select(u => u.UserID)
                .ToListAsync();

            if (adminIds.Count == 0)
                return;

            var audiences = adminIds
                .Select(adminId => new NotificationAudience
                {
                    AudienceType = "USER",
                    AudienceValue = adminId.ToString(),
                })
                .ToList();

            var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
            {
                EventType = "PERMISSION_REQUEST_CREATED",
                EntityType = "PERMISSION_REQUEST",
                EntityId = entity.ID.ToString(),
                Title = "New Permission Request",
                Message = $"{_loginContext.userName} requested {entity.DURATION_MINUTES} min permission " +
                          $"({entity.PERMISSION_DATE:dd-MMM-yyyy}).",
                ActorId = _loginContext.userId,
                ActorName = _loginContext.userName,
                Audiences = audiences,
            });

            foreach (var adminId in adminIds)
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "Notification",
                    Action = "Created",
                    Payload = new { NotificationId = notificationId, CreatedByUserId = _loginContext.userId },
                    KeyField = "NotificationId",
                    TargetUserId = adminId,
                    Timestamp = DateTime.UtcNow,
                });
            }
        }
    }
}
