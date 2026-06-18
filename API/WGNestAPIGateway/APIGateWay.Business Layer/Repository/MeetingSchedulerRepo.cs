using APIGateWay.Business_Layer.Helper.Events.Interface;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class MeetingSchedulerRepo:IMeetingSchedulerRepo
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

        public async Task<GetMeetingDto> CreateMeetingAsync(PostMeetingDto meetingDto)
        {
            GetMeetingDto finalMeetingData = null;

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
                        var internalIds = meetingDto.InternalParticipants.Select(p => p.Id).ToList();

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
                        var clientIds = meetingDto.ClientParticipants.Select(p => p.Id).ToList();

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

                    // ── Step 3: MeetingHistory (Created Event) ─────────────────
                    //{
                    //    var timer = _stepContext.StartStep();
                    //    try
                    //    {
                    //        // Utilizing your history repository to log the creation
                    //        await _historyRepository.LogAsync(MeetingHistoryHelper.MeetingCreated(
                    //            meetingId: meetingMaster.meeting_id,
                    //            title: meetingMaster.title,
                    //            actorId: _loginContext.userId,
                    //            actorName: _loginContext.userName));

                    //        _stepContext.Success("MeetingHistory", "INSERT",
                    //            meetingMaster.meeting_id.ToString(), timer);
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        _stepContext.Failure("MeetingHistory", "INSERT",
                    //            ex.Message, ex.InnerException?.Message, timer);
                    //        throw;
                    //    }
                    //}

                    // Map the finalized entity back to a GET DTO
                    return _mapper.Map<GetMeetingDto>(meetingMaster);
                });
            }
            catch (Exception ex)
            {
                // If anything inside ExecuteInTransactionAsync throws, it rolls back entirely.
                throw new Exception($"Meeting creation failed. Everything was rolled back safely. {ex.Message}", ex);
            }

            // ── Step 4: Publish Event (Fires only if transaction succeeds) ───
            return _mapper.Map<GetMeetingDto>(finalMeetingData);
        }

        public async Task<GetMeetingDto> UpdateMeetingAsync(PutMeetingDto meetingDto)
        {
            GetMeetingDto finalMeetingData = null;

            try
            {
                finalMeetingData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // ── Step 1: Fetch Existing Meeting ──────────────────────────
                    var existingMeeting = _db.MeetingMaster.FirstOrDefault(m => m.meeting_id == meetingDto.Meeting_Id);

                    if (existingMeeting == null)
                        throw new Exception("Meeting not found.");

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
                        existingMeeting.status = meetingDto.Status;

                        existingMeeting.updated_by = _loginContext.userId;
                        existingMeeting.updated_at = DateTime.UtcNow;
                            existingMeeting.days_of_week = meetingDto.Days_Of_Week;

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
                        // 1. Get all current attendees for this meeting
                        var existingAttendees = _db.meeting_attendance.Where(a => a.meeting_id == meetingDto.Meeting_Id).ToList();

                        // 2. Parse incoming IDs from frontend
                        var incomingInternalIds = meetingDto.InternalParticipants?.Select(p => (p.Id)).ToList() ?? new List<Guid>();
                        var incomingClientIds = meetingDto.ClientParticipants?.Select(p => (p.Id)).ToList() ?? new List<Guid>();

                        // Combine all incoming IDs, making sure the Host is safely excluded from standard participant lists
                        var allIncomingIds = incomingInternalIds.Concat(incomingClientIds)
                                                .Where(id => id != existingMeeting.host_id)
                                                .ToList();

                        // 3. REMOVE: Find attendees in DB that are NOT in the incoming list (and are not the Host)
                        var attendeesToRemove = existingAttendees
                            .Where(a => a.participant_role != "Host" && !allIncomingIds.Contains(a.participant_id))
                            .ToList();

                        if (attendeesToRemove.Any())
                        {
                            _db.meeting_attendance.RemoveRange(attendeesToRemove);
                        }

                        // 4. ADD: Find incoming IDs that are NOT currently in the DB
                        var existingParticipantIds = existingAttendees.Select(a => a.participant_id).ToList();
                        var newIdsToAdd = allIncomingIds.Where(id => !existingParticipantIds.Contains(id)).ToList();

                        var newAttendees = new List<MeetingAttendance>();

                        // Re-separate internal vs client for the new additions to map the type correctly
                        foreach (var id in newIdsToAdd)
                        {
                            string type = incomingInternalIds.Contains(id) ? "Employee" : "Client";

                            newAttendees.Add(new MeetingAttendance
                            {
                                meeting_id = existingMeeting.meeting_id,
                                participant_type = type,
                                participant_id = id,
                                participant_role = "Participant",
                                invite_status = "Pending", // New additions start as pending
                                created_by = _loginContext.userId,
                                created_at = DateTime.UtcNow
                            });
                        }

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

            return finalMeetingData;
        }
    }
}
