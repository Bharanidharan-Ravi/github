using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.Session;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
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
    public class LeaveRequestRepo : ILeaveRequestRepo
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

        public LeaveRequestRepo(
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

        public async Task<GetLeaveRequest> CreateLeaveRequestAsync(PostLeaveRequestDto dto)
        {
            GetLeaveRequest finalData = null;
            LeaveRequestMaster entity = null;

            try
            {
                finalData = await _domainService.ExecuteInTransactionAsync<GetLeaveRequest>(async () =>
                {
                    entity = _mapper.Map<LeaveRequestMaster>(dto);
                    entity.ID = Guid.NewGuid();
                    entity.EMPLOYEE_ID = _loginContext.userId;
                    entity.STATUS = "REQUESTED";
                    // Never trust the client-computed day count — recompute from the dates.
                    entity.NO_OF_LEAVE_DAYS =
                        (int)(dto.LeaveTo.Date - dto.LeaveFrom.Date).TotalDays + 1;
                    entity.REQUESTED_DATE =
                        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);

                    await _dBContext.Set<LeaveRequestMaster>().AddAsync(entity);

                    var timer = _stepContext.StartStep();
                    try
                    {
                        await _dBContext.SaveChangesAsync();
                        _stepContext.Success("LeaveRequest", "INSERT", entity.ID.ToString(), timer);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("LeaveRequest", "INSERT",
                            ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }

                    return _mapper.Map<GetLeaveRequest>(entity);
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Leave request creation failed. Everything was rolled back safely. {ex}", ex);
            }

            if (entity != null)
            {
                await NotifyAdminsAsync(entity);
            }

            return finalData;
        }

        public async Task<GetLeaveRequest> UpdateStatusAsync(Guid id, PostLeaveRequestStatusDto dto)
        {
            if (_loginContext.role != 1)
                throw new Exceptionlist.UnauthorizedException("Only admins can approve or reject leave requests.");

            var entity = await _dBContext.Set<LeaveRequestMaster>().FindAsync(id)
                ?? throw new Exceptionlist.DataNotFoundException($"Leave request '{id}' not found.");

            if (entity.STATUS != "REQUESTED")
                throw new Exceptionlist.InvalidDataException("This leave request has already been decided.");

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

                _stepContext.Success("LeaveRequest", "UPDATE", id.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("LeaveRequest", "UPDATE",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

            await NotifyRequesterAsync(entity, dto.Status);
            // Refresh the requester's list and every other admin's list.
            await BroadcastLeaveRequestChangeAsync(entity, "Updated", entity.EMPLOYEE_ID);

            return _mapper.Map<GetLeaveRequest>(entity);
        }

        private Task BroadcastLeaveRequestChangeAsync(LeaveRequestMaster entity, string action, Guid? targetUserId)
        {
            return _realtimeNotifier.BroadcastAsync(new RealtimeMessage
            {
                Entity = "LeaveRequest",
                Action = action,
                Payload = new { ID = entity.ID, EMPLOYEE_ID = entity.EMPLOYEE_ID, STATUS = entity.STATUS },
                KeyField = "ID",
                TargetUserId = targetUserId,
                Timestamp = DateTime.UtcNow,
            });
        }

        private async Task NotifyRequesterAsync(LeaveRequestMaster entity, string status)
        {
            var approved = status == "APPROVED";
            var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
            {
                EventType = approved ? "LEAVE_REQUEST_APPROVED" : "LEAVE_REQUEST_REJECTED",
                EntityType = "LEAVE_REQUEST",
                EntityId = entity.ID.ToString(),
                Title = approved ? "Leave Request Approved" : "Leave Request Rejected",
                Message = approved
                    ? $"Your leave request ({entity.LEAVE_FROM:dd-MMM-yyyy} - {entity.LEAVE_TO:dd-MMM-yyyy}) was approved."
                    : $"Your leave request ({entity.LEAVE_FROM:dd-MMM-yyyy} - {entity.LEAVE_TO:dd-MMM-yyyy}) was rejected. Reason: {entity.REJECT_REASON}",
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

        private async Task NotifyAdminsAsync(LeaveRequestMaster entity)
        {
            // Data ping first: RealtimeNotifier sends non-"Notification" entities to the
            // "global-admin" SignalR group (joined from the JWT role), so every admin's
            // Leave Requests list refreshes even if the lookup below finds nobody.
            await BroadcastLeaveRequestChangeAsync(entity, "Created", null);

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
                EventType = "LEAVE_REQUEST_CREATED",
                EntityType = "LEAVE_REQUEST",
                EntityId = entity.ID.ToString(),
                Title = "New Leave Request",
                Message = $"{_loginContext.userName} requested {entity.NO_OF_LEAVE_DAYS} day(s) leave " +
                          $"({entity.LEAVE_FROM:dd-MMM-yyyy} - {entity.LEAVE_TO:dd-MMM-yyyy}).",
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
