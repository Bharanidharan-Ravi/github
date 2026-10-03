using APIGateWay.Business_Layer.Helper.Events.EventFactory;
using APIGateWay.Business_Layer.Helper.Events.Interface;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using APIGateWay.ModelLayer.ErrorException;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class MeetingSchedulerRepo : IMeetingSchedulerRepo
    {
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly IWorkStreamService _workStreamService;
        private readonly APIGatewayDBContext _db;
        private readonly ITicketHistoryRepository _historyRepository;
        private readonly IRequestStepContext _stepContext;
        private readonly IEventCenter _eventCenter;


        public MeetingSchedulerRepo(IDomainService domainService, APIGateWayCommonService commonService, IMapper mapper, ILoginContextService loginContext, IAttachmentService attachmentService, IHelperGetData helperGet, IRealtimeNotifier realtimeNotifier, ISyncExecutionService syncExecutionService, IWorkStreamService workStreamService, APIGatewayDBContext db, ITicketHistoryRepository historyRepository, IRequestStepContext stepContext, IEventCenter eventCenter)
        {
            _domainService = domainService;
            _commonService = commonService;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _syncExecutionService = syncExecutionService;
            _workStreamService = workStreamService;
            _db = db;
            _historyRepository = historyRepository;
            _stepContext = stepContext;
            _eventCenter = eventCenter;
        }

        // Times per day is disabled for now.
        // "9:30" / "09:30:00" -> TimeSpan; empty -> null (same result AutoMapper gives on create)
        //private static TimeSpan? ParseTimeOrNull(string? value) =>
        //    string.IsNullOrWhiteSpace(value)
        //        ? null
        //        : TimeSpan.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        public async Task<GetMeetingDto> CreateMeetingAsync(PostMeetingDto meetingDto)
        {
            GetMeetingDto? finalMeetingData = null;
            MeetingMaster? meetingMasterForThread = null;
            var attendeesForThread = new List<MeetingAttendance>();

            try
            {
                finalMeetingData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // ── Initialize and Map ─────────────────────────────────────
                    var meetingMaster = _mapper.Map<MeetingMaster>(meetingDto);

                    // Set fundamental properties
                    meetingMaster.meeting_id = Guid.NewGuid();
                    meetingMaster.status = "Scheduled"; // Default starting status
                    meetingMaster.created_by = _loginContext.userId;
                    meetingMaster.created_at = DateTime.UtcNow;
                    meetingMasterForThread = meetingMaster;
                    // ── Step 1: Insert MeetingMaster ───────────────────────────
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            // Assuming a SaveMeetingAsync method exists in your domain service
                            _db.MeetingMaster.Add(meetingMaster);
                            await _db.SaveChangesAsync();

                            _stepContext.Success("MeetingMaster", "INSERT",
                                meetingMaster.meeting_id.ToString(), timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("MeetingMaster", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }

                    // ── Step 2: Insert Meeting Attendance (Participants) ───────
                    var attendees = new List<MeetingAttendance>();

                    // 2a. Add Host automatically as an attendee (Present/Accepted)
                    if (meetingMaster.host_id != Guid.Empty)
                    {
                        attendees.Add(new MeetingAttendance
                        {
                            meeting_id = meetingMaster.meeting_id,
                            participant_type = meetingMaster.host_type, // 'Employee' or 'Client'
                            participant_id = meetingMaster.host_id,
                            participant_role = "Host",
                            invite_status = "Accepted",
                            attendance_status = "Present",
                            created_by = _loginContext.userId,
                            created_at = DateTime.UtcNow
                        });
                    }

                    // 2b. Add Internal Participants (Employees)
                    if (meetingDto.InternalParticipants != null && meetingDto.InternalParticipants.Any())
                    {
                        var internalIds = meetingDto.InternalParticipants.Select(p => p.Id).Distinct().ToList();

                        // Exclude host if they were somehow selected in the dropdown to prevent duplicates
                        attendees.AddRange(internalIds.Where(id => id != meetingMaster.host_id).Select(id => new MeetingAttendance
                        {
                            meeting_id = meetingMaster.meeting_id,
                            participant_type = "Employee",
                            participant_id = id,
                            participant_role = "Participant",
                            invite_status = "Pending",
                            created_by = _loginContext.userId,
                            created_at = DateTime.UtcNow
                        }));
                    }

                    // 2c. Add Client Participants (External)
                    if (meetingDto.ClientParticipants != null && meetingDto.ClientParticipants.Any())
                    {
                        var clientIds = meetingDto.ClientParticipants.Select(p => p.Id).Distinct().ToList();

                        attendees.AddRange(clientIds.Where(id => id != meetingMaster.host_id).Select(id => new MeetingAttendance
                        {
                            meeting_id = meetingMaster.meeting_id,
                            participant_type = "Client",
                            participant_id = id,
                            participant_role = "Participant",
                            invite_status = "Pending",
                            created_by = _loginContext.userId,
                            created_at = DateTime.UtcNow
                        }));
                    }

                    if (attendees.Any())
                    {
                        var timer = _stepContext.StartStep();
                        try
                        {
                            // Assuming SaveMeetingAttendanceAsync accepts a List<MeetingAttendance>
                            _db.meeting_attendance.AddRange(attendees);
                            await _db.SaveChangesAsync();

                            var attendeeIds = string.Join(",", attendees.Select(a => a.participant_id));
                            _stepContext.Success("MeetingAttendance", "INSERT", attendeeIds, timer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("MeetingAttendance", "INSERT",
                                ex.Message, ex.InnerException?.Message, timer);
                            throw;
                        }
                    }
                    attendeesForThread = attendees;
                    // Map the finalized entity back to a GET DTO
                    return _mapper.Map<GetMeetingDto>(meetingMaster);
                });
            }
            catch (Exception ex)
            {
                // If anything inside ExecuteInTransactionAsync throws, it rolls back entirely.
                throw new Exception($"Meeting creation failed. Everything was rolled back safely. {ex.Message}", ex);
            }

            // ── Step 3: Post the ticket thread (meeting is already saved) ───
            if (finalMeetingData.Ticket_Id.HasValue && meetingMasterForThread != null)
            {
                finalMeetingData.ThreadId =
                    await TryCreateMeetingThreadAsync(meetingMasterForThread, attendeesForThread);
            }

            // ── Step 4: Publish Event (Fires only if transaction succeeds) ───
            await _eventCenter.PublishAsync<GetMeetingDto>(
                TicketFactory.MeetingCreated(
                    finalMeetingData.Meeting_Id, finalMeetingData.Title, notifyUsers: true));

            return finalMeetingData;
        }

        public async Task<GetMeetingDto> UpdateMeetingAsync(Guid id, PutMeetingDto meetingDto)
        {
            // ── Step 1: Fetch Existing Meeting ──────────────────────────────
            var existingMeeting = await _db.MeetingMaster.FirstOrDefaultAsync(m => m.meeting_id == id)
                ?? throw new Exceptionlist.DataNotFoundException("Meeting not found.");

            // The UI only shows Edit to the host; enforce it here too.
            if (existingMeeting.host_id != _loginContext.userId)
                throw new Exceptionlist.InvalidDataException("Only the host can edit this meeting.");

            // The UI hides Edit for these; enforce it here too. Saving would also turn the
            // completed ticket thread back into "Meeting Scheduled" and clear its hours.
            if (existingMeeting.status is "Completed" or "Cancelled")
                throw new Exceptionlist.InvalidDataException(
                    $"This meeting is already {existingMeeting.status.ToLowerInvariant()} and can't be edited.");

            GetMeetingDto? finalMeetingData = null;

            try
            {
                finalMeetingData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // ── Step 2: Update MeetingMaster Fields ─────────────────────
                    var timerMaster = _stepContext.StartStep();
                    try
                    {
                        existingMeeting.title = meetingDto.Title;
                        existingMeeting.host_type = meetingDto.Host_Type;
                        existingMeeting.host_id = meetingDto.Host_Id;
                        existingMeeting.booking_type = meetingDto.Booking_Type;
                        existingMeeting.project_id = meetingDto.Project_Id;
                        existingMeeting.meeting_summary = meetingDto.Meeting_Summary;
                        existingMeeting.slot_duration = meetingDto.Slot_Duration;
                        existingMeeting.recurrence_type = meetingDto.Recurrence_Type;
                        existingMeeting.meeting_date = meetingDto.Meeting_Date;
                        existingMeeting.valid_from_date = meetingDto.Valid_From_Date;
                        existingMeeting.valid_to_date = meetingDto.Valid_To_Date;
                        existingMeeting.start_time = meetingDto.Start_Time;
                        existingMeeting.end_time = meetingDto.End_Time;
                        existingMeeting.updated_by = _loginContext.userId;
                        existingMeeting.updated_at = DateTime.UtcNow;
                        existingMeeting.days_of_week = meetingDto.Days_Of_Week;
                        // Times per day is disabled for now.
                        //existingMeeting.times_per_day = meetingDto.Times_Per_Day;
                        //existingMeeting.second_start_time = ParseTimeOrNull(meetingDto.Second_Start_Time);
                        //existingMeeting.second_end_time = ParseTimeOrNull(meetingDto.Second_End_Time);
                        existingMeeting.meet_method = meetingDto.Meet_Method;
                        existingMeeting.meet_link = meetingDto.Meet_Link;
                        existingMeeting.meet_password = meetingDto.Meet_Password;

                        _db.MeetingMaster.Update(existingMeeting);
                        await _db.SaveChangesAsync();

                        _stepContext.Success("MeetingMaster", "UPDATE", existingMeeting.meeting_id.ToString(), timerMaster);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("MeetingMaster", "UPDATE", ex.Message, ex.InnerException?.Message, timerMaster);
                        throw;
                    }

                    // ── Step 3: Handle Attendees (Smart Update) ─────────────────
                    var timerAtt = _stepContext.StartStep();
                    try
                    {
                        var hostId = existingMeeting.host_id;

                        // 1. Get all current attendees for this meeting
                        var existingAttendees = await _db.meeting_attendance
                            .Where(a => a.meeting_id == id)
                            .ToListAsync();

                        // 2. Parse incoming IDs from frontend
                        var incomingInternalIds = meetingDto.InternalParticipants?.Select(p => p.Id).ToList() ?? new List<Guid>();
                        var incomingClientIds = meetingDto.ClientParticipants?.Select(p => p.Id).ToList() ?? new List<Guid>();

                        // Everyone invited except the host, who is handled in step 4
                        var allIncomingIds = incomingInternalIds.Concat(incomingClientIds)
                                                .Where(pid => pid != hostId)
                                                .Distinct()
                                                .ToList();

                        // 3. REMOVE: attendees no longer invited. After a host change this
                        //    includes the previous host, unless they are still invited.
                        var attendeesToRemove = existingAttendees
                            .Where(a => a.participant_id != hostId && !allIncomingIds.Contains(a.participant_id))
                            .ToList();

                        if (attendeesToRemove.Any())
                        {
                            _db.meeting_attendance.RemoveRange(attendeesToRemove);
                        }

                        foreach (var formerHost in existingAttendees.Where(a =>
                                     a.participant_role == "Host" &&
                                     a.participant_id != hostId &&
                                     allIncomingIds.Contains(a.participant_id)))
                        {
                            formerHost.participant_role = "Participant";
                        }

                        // 4. HOST: add the host's row, or promote their participant row after a host change
                        var hostRow = existingAttendees.FirstOrDefault(a => a.participant_id == hostId);

                        if (hostRow == null && hostId != Guid.Empty)
                        {
                            _db.meeting_attendance.Add(new MeetingAttendance
                            {
                                meeting_id = existingMeeting.meeting_id,
                                participant_type = existingMeeting.host_type,
                                participant_id = hostId,
                                participant_role = "Host",
                                invite_status = "Accepted",
                                attendance_status = "Present",
                                created_by = _loginContext.userId,
                                created_at = DateTime.UtcNow
                            });
                        }
                        else if (hostRow != null && hostRow.participant_role != "Host")
                        {
                            hostRow.participant_type = existingMeeting.host_type;
                            hostRow.participant_role = "Host";
                            hostRow.invite_status = "Accepted";
                            hostRow.attendance_status = "Present";
                        }

                        // 5. ADD: invited IDs that are NOT currently in the DB
                        var existingParticipantIds = existingAttendees.Select(a => a.participant_id).ToList();

                        var newAttendees = allIncomingIds
                            .Where(pid => !existingParticipantIds.Contains(pid))
                            .Select(pid => new MeetingAttendance
                            {
                                meeting_id = existingMeeting.meeting_id,
                                participant_type = incomingInternalIds.Contains(pid) ? "Employee" : "Client",
                                participant_id = pid,
                                participant_role = "Participant",
                                invite_status = "Pending", // New additions start as pending
                                created_by = _loginContext.userId,
                                created_at = DateTime.UtcNow
                            })
                            .ToList();

                        if (newAttendees.Any())
                        {
                            _db.meeting_attendance.AddRange(newAttendees);
                        }

                        await _db.SaveChangesAsync();

                        _stepContext.Success("MeetingAttendance", "UPDATE", existingMeeting.meeting_id.ToString(), timerAtt);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("MeetingAttendance", "UPDATE", ex.Message, ex.InnerException?.Message, timerAtt);
                        throw;
                    }

                    // Return the mapped object
                    return _mapper.Map<GetMeetingDto>(existingMeeting);
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Meeting update failed. Everything was rolled back safely. {ex.Message}", ex);
            }

            if (existingMeeting.ticket_id.HasValue)
            {
                var attendance = await _db.meeting_attendance
                    .Where(x => x.meeting_id == existingMeeting.meeting_id)
                    .ToListAsync();

                if (existingMeeting.ThreadId.HasValue)
                {
                    await _workStreamService.UpdateMeetingThreadAsync(
                        existingMeeting,
                        attendance,
                        _loginContext.userId);

                    await _eventCenter.PublishAsync<ThreadList>(
                        TicketFactory.ThreadUpdated(
                            existingMeeting.ticket_id.Value,
                            existingMeeting.ThreadId.Value));
                }
                else
                {
                    // The thread wasn't posted when the meeting was created; post it now.
                    finalMeetingData.ThreadId =
                        await TryCreateMeetingThreadAsync(existingMeeting, attendance);
                }
            }

            // Refreshes the open scheduler screens and notifies the host and participants.
            await _eventCenter.PublishAsync<GetMeetingDto>(
                TicketFactory.MeetingUpdated(existingMeeting.meeting_id, existingMeeting.title, notifyUsers: true));

            return finalMeetingData;
        }

        public async Task CompleteMeetingAsync(MeetingCompletionDto dto, Guid userId)
        {
            var meeting = await _db.MeetingMaster.FirstOrDefaultAsync(m => m.meeting_id == dto.MeetingId)
                ?? throw new Exceptionlist.DataNotFoundException("Meeting not found.");

            if (meeting.status is "Completed" or "Cancelled")
                throw new Exceptionlist.InvalidDataException($"This meeting is already {meeting.status.ToLowerInvariant()}.");

            if (dto.ActualEndTime <= dto.ActualStartTime)
                throw new Exceptionlist.InvalidDataException("End Time must be after Start Time.");

            // A day comment on a DAILY / WEEKLY meeting: post it as a new thread on
            // the ticket (one day can have several). The meeting stays Scheduled
            // unless EndSeries is set.
            var loggedDay = dto.OccurrenceDate.HasValue && IsRecurring(meeting);

            if (loggedDay)
            {
                ValidateOccurrence(meeting, dto);

                var threadId = await _workStreamService.PostMeetingOccurrenceThreadAsync(meeting, dto, userId);

                await _eventCenter.PublishAsync<ThreadList>(
                    TicketFactory.ThreadCreated(meeting.ticket_id!.Value, threadId));

                if (!dto.EndSeries)
                    return;
            }

            var summary = dto.MeetingSummary ?? string.Empty;
            var now = IndiaNow();

            // The form sends times on today's date; for a meeting day, move them onto it.
            var day = dto.OccurrenceDate?.Date;
            var actualStart = day.HasValue ? day.Value + dto.ActualStartTime.TimeOfDay : dto.ActualStartTime;
            var actualEnd = day.HasValue ? day.Value + dto.ActualEndTime.TimeOfDay : dto.ActualEndTime;

            await _domainService.ExecuteInTransactionAsync(async () =>
            {
                var timer = _stepContext.StartStep();
                try
                {
                    _db.MeetingCompletion.Add(new MeetingCompletion
                    {
                        completion_id = Guid.NewGuid(),
                        meeting_id = meeting.meeting_id,
                        actual_start_time = actualStart,
                        actual_end_time = actualEnd,
                        duration_minutes = (int)(actualEnd - actualStart).TotalMinutes,
                        meeting_summary = summary,
                        completedby = userId,
                        completedat = now,
                        createdAt = now,
                        updatedAt = now,
                        occurrence_date = day
                            ?? (IsRecurring(meeting) ? now.Date : (meeting.meeting_date ?? now).Date),
                    });

                    meeting.meeting_summary = summary;
                    meeting.status = "Completed";
                    meeting.updated_by = userId;
                    meeting.updated_at = now;

                    await UpdateAttendanceAsync(dto, now);

                    await _db.SaveChangesAsync();

                    _stepContext.Success("MeetingCompletion", "INSERT", meeting.meeting_id.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("MeetingCompletion", "INSERT",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                return true;
            });

            if (meeting.ticket_id.HasValue && meeting.ThreadId.HasValue)
            {
                // The day's own thread already holds its times and hours; copying them
                // onto the main thread as well would count that day twice.
                if (loggedDay)
                    await _workStreamService.CloseMeetingSeriesThreadAsync(meeting, day!.Value, userId);
                else
                    await _workStreamService.UpdateMeetingCompletionThreadAsync(meeting, dto, userId);

                await _eventCenter.PublishAsync<ThreadList>(
                    TicketFactory.ThreadUpdated(meeting.ticket_id.Value, meeting.ThreadId.Value));
            }

            await _eventCenter.PublishAsync<GetMeetingDto>(
                TicketFactory.MeetingCompleted(meeting.meeting_id, meeting.title, notifyUsers: true));
        }

        public async Task CancelMeetingAsync(Guid meetingId, Guid userId)
        {
            var meeting = await _db.MeetingMaster.FirstOrDefaultAsync(m => m.meeting_id == meetingId)
                ?? throw new Exceptionlist.DataNotFoundException("Meeting not found.");

            // The UI only shows Cancel to the host; enforce it here too.
            if (meeting.host_id != userId)
                throw new Exceptionlist.InvalidDataException("Only the host can cancel this meeting.");

            if (meeting.status is "Completed" or "Cancelled")
                throw new Exceptionlist.InvalidDataException($"This meeting is already {meeting.status.ToLowerInvariant()}.");

            meeting.status = "Cancelled";
            meeting.updated_by = userId;
            meeting.updated_at = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Refreshes the open scheduler screens and notifies the host and participants.
            await _eventCenter.PublishAsync<GetMeetingDto>(
                TicketFactory.MeetingCancelled(meeting.meeting_id, meeting.title, notifyUsers: true));
        }

        // Posts the "Meeting Scheduled" thread on the meeting's ticket and stores its id.
        // The meeting is already saved, so a failure is recorded instead of thrown:
        // failing the request would make the user retry and create the meeting twice.
        private async Task<long?> TryCreateMeetingThreadAsync(
            MeetingMaster meeting,
            List<MeetingAttendance> attendance)
        {
            var timer = _stepContext.StartStep();
            try
            {
                var threadId = await _workStreamService.PostMeetingScheduledAsync(
                    meeting,
                    attendance,
                    _loginContext.userId);

                await _domainService.UpdateTrackedEntityAsync<MeetingMaster>(
                    x => x.meeting_id == meeting.meeting_id,
                    x => x.ThreadId = threadId);

                meeting.ThreadId = threadId;

                _stepContext.Success("MeetingMaster", "UPDATE", threadId.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("MeetingMaster", "UPDATE",
                    ex.Message, ex.InnerException?.Message, timer);
                return null;
            }

            await _eventCenter.PublishAsync<ThreadList>(
                TicketFactory.ThreadCreated(meeting.ticket_id!.Value, meeting.ThreadId.Value));

            return meeting.ThreadId;
        }

        private static bool IsRecurring(MeetingMaster meeting) =>
            meeting.recurrence_type?.ToUpperInvariant() is "DAILY" or "WEEKLY";

        private static DateTime IndiaNow() =>
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

        private static void ValidateOccurrence(MeetingMaster meeting, MeetingCompletionDto dto)
        {
            if (!meeting.ticket_id.HasValue)
                throw new Exceptionlist.InvalidDataException("Link a ticket to this meeting to record comments for each day.");

            var day = dto.OccurrenceDate!.Value.Date;

            if (day > IndiaNow().Date)
                throw new Exceptionlist.InvalidDataException("You can't add a comment for a day that hasn't happened yet.");

            if ((meeting.valid_from_date.HasValue && day < meeting.valid_from_date.Value.Date) ||
                (meeting.valid_to_date.HasValue && day > meeting.valid_to_date.Value.Date))
                throw new Exceptionlist.InvalidDataException($"{day:dd MMM yyyy} is outside this meeting's dates.");

            // days_of_week is a 7-char bitmask, index 0 = Sunday (same as DayOfWeek).
            if (meeting.recurrence_type.ToUpperInvariant() == "WEEKLY" &&
                meeting.days_of_week?.ElementAtOrDefault((int)day.DayOfWeek) != '1')
                throw new Exceptionlist.InvalidDataException($"This meeting doesn't run on {day:dddd}s.");
        }

        // Changes the tracked rows only; the caller saves them with the completion.
        private async Task UpdateAttendanceAsync(MeetingCompletionDto dto, DateTime now)
        {
            if (dto.Attendance == null || !dto.Attendance.Any())
                return;

            var byParticipant = dto.Attendance
                .GroupBy(x => x.ParticipantId)
                .ToDictionary(g => g.Key, g => g.First());
            var participantIds = byParticipant.Keys.ToList();

            var attendanceList = await _db.meeting_attendance
                .Where(x =>
                    x.meeting_id == dto.MeetingId &&
                    participantIds.Contains(x.participant_id))
                .ToListAsync();

            foreach (var attendance in attendanceList)
            {
                var dtoAttendance = byParticipant[attendance.participant_id];

                attendance.attendance_status = dtoAttendance.AttendanceStatus;
                attendance.invite_status = dtoAttendance.InviteStatus;
                attendance.response_date = now;
                attendance.remark = dtoAttendance.Remark;
            }
        }
    }
}
