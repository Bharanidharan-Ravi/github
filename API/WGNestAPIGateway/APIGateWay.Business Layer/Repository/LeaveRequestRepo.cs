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
using System.Text.Json;
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
            if (!AppRoles.LeaveRequestCreate.Contains(_loginContext.role))
                throw new Exceptionlist.UnauthorizedException("Only employees and admins can submit leave requests.");

            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);
            var days = BuildLeaveDays(dto.LeaveFrom, dto.LeaveTo, dto.Days, nowIst, allowPast: false);
            await EnsureNoOverlapAsync(_loginContext.userId, days);

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
                    // Never trust the client-computed day count — recompute from the day sessions.
                    entity.NO_OF_LEAVE_DAYS = days.Sum(d => LeaveDaySession.Days(d.DAY_SESSION));
                    entity.REQUESTED_DATE = nowIst;

                    days.ForEach(d => d.LEAVE_REQUEST_ID = entity.ID);

                    await _dBContext.Set<LeaveRequestMaster>().AddAsync(entity);
                    await _dBContext.Set<LeaveRequestDayMaster>().AddRangeAsync(days);

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

                    var result = _mapper.Map<GetLeaveRequest>(entity);
                    result.DAYS_JSON = JsonSerializer.Serialize(days.Select(d => new
                    {
                        date = d.LEAVE_DATE.ToString("yyyy-MM-dd"),
                        session = d.DAY_SESSION,
                    }));
                    return result;
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

        // allowPast: admins re-adjusting an existing request may move it onto past
        // dates, and the "only 2nd half left today" rule doesn't apply to them.
        private static List<LeaveRequestDayMaster> BuildLeaveDays(
            DateTime leaveFrom, DateTime leaveTo, List<PostLeaveDayDto>? requestedDays, DateTime nowIst, bool allowPast)
        {
            var from = leaveFrom.Date;
            var to = leaveTo.Date;
            var today = nowIst.Date;

            if (to < from)
                throw new Exceptionlist.InvalidDataException("To Date cannot be before From Date.");
            if (!allowPast && from < today)
                throw new Exceptionlist.InvalidDataException("Leave cannot start on a past date.");

            var requested = new Dictionary<DateTime, string>();
            foreach (var day in requestedDays ?? new List<PostLeaveDayDto>())
            {
                var date = day.Date.Date;
                if (date < from || date > to)
                    throw new Exceptionlist.InvalidDataException($"{date:dd-MMM-yyyy} is outside the selected leave dates.");
                if (!LeaveDaySession.All.Contains(day.Session))
                    throw new Exceptionlist.InvalidDataException($"Invalid session '{day.Session}' for {date:dd-MMM-yyyy}.");
                if (!requested.TryAdd(date, day.Session))
                    throw new Exceptionlist.InvalidDataException($"{date:dd-MMM-yyyy} is listed more than once.");
            }

            var days = new List<LeaveRequestDayMaster>();
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var session = requested.TryGetValue(date, out var s) ? s : LeaveDaySession.Full;

                if (!allowPast && date == today && nowIst.TimeOfDay >= LeaveDaySession.FirstHalfEnd && session != LeaveDaySession.SecondHalf)
                    throw new Exceptionlist.InvalidDataException("Only the 2nd half (2:00 PM – 6:30 PM) is left for today.");

                days.Add(new LeaveRequestDayMaster
                {
                    ID = Guid.NewGuid(),
                    LEAVE_DATE = date,
                    DAY_SESSION = session,
                });
            }

            return days;
        }

        // A day clashes when either side is FULL, or both are the same half.
        // excludeRequestId: the request being re-adjusted, so it doesn't clash with itself.
        private async Task EnsureNoOverlapAsync(Guid employeeId, List<LeaveRequestDayMaster> days, Guid? excludeRequestId = null)
        {
            var rangeStart = days.First().LEAVE_DATE;
            var rangeEnd = days.Last().LEAVE_DATE;

            var taken = await (
                from d in _dBContext.Set<LeaveRequestDayMaster>()
                join r in _dBContext.Set<LeaveRequestMaster>() on d.LEAVE_REQUEST_ID equals r.ID
                where r.EMPLOYEE_ID == employeeId
                      && r.ID != excludeRequestId
                      && (r.STATUS == "REQUESTED" || (r.STATUS == "APPROVED" && !r.NOT_TAKEN))
                      && d.LEAVE_DATE >= rangeStart && d.LEAVE_DATE <= rangeEnd
                select new { d.LEAVE_DATE, d.DAY_SESSION }
            ).ToListAsync();

            foreach (var day in days)
            {
                var clash = taken.FirstOrDefault(t =>
                    t.LEAVE_DATE.Date == day.LEAVE_DATE &&
                    (t.DAY_SESSION == LeaveDaySession.Full
                     || day.DAY_SESSION == LeaveDaySession.Full
                     || t.DAY_SESSION == day.DAY_SESSION));

                if (clash != null)
                    throw new Exceptionlist.InvalidDataException(
                        $"You already have leave on {day.LEAVE_DATE:dd-MMM-yyyy} ({LeaveDaySession.Label(clash.DAY_SESSION)}).");
            }
        }

        public async Task<GetLeaveRequest> UpdateStatusAsync(Guid id, PostLeaveRequestStatusDto dto)
        {
            if (_loginContext.role != 1)
                throw new Exceptionlist.UnauthorizedException("Only admins can approve or reject leave requests.");

            var entity = await _dBContext.Set<LeaveRequestMaster>().FindAsync(id)
                ?? throw new Exceptionlist.DataNotFoundException($"Leave request '{id}' not found.");

            // Admins may re-decide an already decided request (re-approve a "not taken"
            // or rejected leave, reject an approved one) and re-adjust its dates.
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);
            var existingDays = await _dBContext.Set<LeaveRequestDayMaster>()
                .Where(d => d.LEAVE_REQUEST_ID == id)
                .ToListAsync();

            List<LeaveRequestDayMaster>? newDays = null;
            if (dto.LeaveFrom.HasValue && dto.LeaveTo.HasValue)
            {
                newDays = BuildLeaveDays(dto.LeaveFrom.Value, dto.LeaveTo.Value, dto.Days, now, allowPast: true);
                newDays.ForEach(d => d.LEAVE_REQUEST_ID = id);
            }

            // Only an approved leave occupies the calendar, so only check clashes then.
            var effectiveDays = newDays ?? existingDays;
            if (dto.Status == "APPROVED" && effectiveDays.Count > 0)
                await EnsureNoOverlapAsync(entity.EMPLOYEE_ID, effectiveDays.OrderBy(d => d.LEAVE_DATE).ToList(), id);

            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    if (newDays != null)
                    {
                        _dBContext.Set<LeaveRequestDayMaster>().RemoveRange(existingDays);
                        await _dBContext.Set<LeaveRequestDayMaster>().AddRangeAsync(newDays);
                        entity.LEAVE_FROM = newDays.First().LEAVE_DATE;
                        entity.LEAVE_TO = newDays.Last().LEAVE_DATE;
                        entity.NO_OF_LEAVE_DAYS = newDays.Sum(d => LeaveDaySession.Days(d.DAY_SESSION));
                    }

                    entity.STATUS = dto.Status;
                    // A fresh decision resets any earlier "not taken" mark.
                    entity.NOT_TAKEN = false;
                    entity.NOT_TAKEN_BY = null;
                    entity.NOT_TAKEN_DATE = null;

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

        public async Task<GetLeaveRequest> MarkNotTakenAsync(Guid id)
        {
            if (_loginContext.role != 1)
                throw new Exceptionlist.UnauthorizedException("Only admins can mark a leave as not taken.");

            var entity = await _dBContext.Set<LeaveRequestMaster>().FindAsync(id)
                ?? throw new Exceptionlist.DataNotFoundException($"Leave request '{id}' not found.");

            if (entity.STATUS != "APPROVED")
                throw new Exceptionlist.InvalidDataException("Only an approved leave request can be marked as not taken.");
            if (entity.NOT_TAKEN)
                throw new Exceptionlist.InvalidDataException("This leave request has already been marked as not taken.");
            // "Not taken" is only known once the leave is over; before that the admin rejects it instead.
            var todayIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone).Date;
            if (entity.LEAVE_TO.Date >= todayIst)
                throw new Exceptionlist.InvalidDataException("A leave can only be marked as not taken after its last day is over. Reject it instead.");

            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    entity.NOT_TAKEN = true;
                    entity.NOT_TAKEN_BY = _loginContext.userId;
                    entity.NOT_TAKEN_DATE = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone);

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

            await NotifyNotTakenAsync(entity);
            await BroadcastLeaveRequestChangeAsync(entity, "Updated", entity.EMPLOYEE_ID);

            return _mapper.Map<GetLeaveRequest>(entity);
        }

        private async Task NotifyNotTakenAsync(LeaveRequestMaster entity)
        {
            var notificationId = await _notificationRepository.CreateAsync(new CreateNotificationRequest
            {
                EventType = "LEAVE_REQUEST_NOT_TAKEN",
                EntityType = "LEAVE_REQUEST",
                EntityId = entity.ID.ToString(),
                Title = "Leave Marked As Not Taken",
                Message = $"Your leave request ({entity.LEAVE_FROM:dd-MMM-yyyy} - {entity.LEAVE_TO:dd-MMM-yyyy}) was marked as not taken.",
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
                Message = $"{_loginContext.userName} requested {entity.NO_OF_LEAVE_DAYS:0.#} day(s) leave " +
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
