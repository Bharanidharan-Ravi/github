using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ReverseMarkdown.Converters;

namespace APIGateWay.Business_Layer.Repository
{
    public class MeetingRepo:IMeetingRepo
    {
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly APIGatewayDBContext _dBContext;
        private readonly IWorkStreamService _workStreamService;
        private readonly IRequestStepContext _stepContext;            // ← ADDED

        public MeetingRepo(
            IDomainService domainService,
            APIGateWayCommonService service,
            APIGatewayDBContext dbContext,
            IMapper mapper,
            ILoginContextService loginContext,
            IAttachmentService attachmentService,
            IHelperGetData helperGet,
            IRealtimeNotifier realtimeNotifier,
            ISyncExecutionService syncExecutionService,
            IWorkStreamService workStreamService,
            IRequestStepContext stepContext)                          // ← ADDED
        {
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _syncExecutionService = syncExecutionService;
            _dBContext = dbContext;
            _workStreamService = workStreamService;
            _stepContext = stepContext;                      // ← ADDED
        }

        //public async Task<GetMeeting> CreateMeetingAsync(PostingmeetingDto dto)
        //{
        //    GetMeeting finalMeetingData = null;
        //    try
        //    {
        //        finalMeetingData = await _domainService.ExecuteInTransactionAsync(async () =>
        //        {
        //            var meeting = _mapper.Map<MeetingMaster>(dto);
        //            meeting.meeting_id = Guid.NewGuid();
        //            meeting.status = "Active";
        //            meeting.created_by = _loginContext.userId;

        //            meeting.created_at=DateTime.UtcNow;
        //            meeting.updated_at=DateTime.UtcNow;
        //            meeting.updated_by=_loginContext.userId;

        //            var timer = _stepContext.StartStep();
        //            try
        //            {
        //                await _dBContext.Set<MeetingMaster>().AddAsync(meeting);
        //                await _dBContext.SaveChangesAsync();
        //                _stepContext.Success("MeetingMaster", "INSERT", meeting.meeting_id.ToString(), timer);
        //            }
        //            catch (Exception ex)
        //            {
        //                _stepContext.Failure("MeetingMaster", "INSERT",
        //                    ex.Message, ex.InnerException?.Message, timer);
        //                throw;
        //            }

        //            return _mapper.Map<GetMeeting>(meeting);

        //        });
        //    }
        //    catch(Exception ex) {
        //        throw new Exception($"Meeting creation failed.Everything was rolled back safely.{ex}", ex);
        //    }

        //    return finalMeetingData;
        //}
        public async Task<GetMeeting> CreateMeetingAsync(PostingmeetingDto dto)
        {
            GetMeeting finalMeetingData = null;

            try
            {
                finalMeetingData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // 1. Map DTO to MeetingMaster
                    var meeting = _mapper.Map<MeetingMaster>(dto);
                    meeting.meeting_id = Guid.NewGuid();
                    meeting.status = "Active";
                    meeting.created_by = _loginContext.userId;
                    meeting.created_at = DateTime.UtcNow;
                    meeting.updated_at = DateTime.UtcNow;
                    var timer = _stepContext.StartStep();

                    try
                    {
                        await _dBContext.MeetingMaster.AddAsync(meeting);
                        await _dBContext.SaveChangesAsync();
                        _stepContext.Success("MeetingMaster", "INSERT", meeting.meeting_id.ToString(), timer);
                    }
                    catch (Exception ex)
                    {
                        _stepContext.Failure("MeetingMaster", "INSERT",
                            ex.Message, ex.InnerException?.Message, timer);
                        throw;
                    }
                    var hosterId = meeting.host_id;

                    // 2. Insert Host as participant
                    var attendances = new List<MeetingAttendance>();
                    if (dto.internalParticipants?.Any() == true)
                    {
                        attendances.AddRange(
                            dto.internalParticipants
                                .Where(p => p.Id.HasValue)
                                .Select(p => BuildAttendance(
                                    hosterId,
                                    meeting.meeting_id,
                                    "Internal",
                                    p.Id!.Value
                                ))
                        );
                    }
                    if (dto.clientParticipants?.Any() == true)
                    {
                        attendances.AddRange(
                            dto.clientParticipants
                                .Where(p => p.Id.HasValue)
                                .Select(p => BuildAttendance(
                                    hosterId,
                                    meeting.meeting_id,
                                    "Client",
                                    p.Id!.Value
                                ))
                        );
                    }


                    if (attendances.Any())
                    {
                        await _dBContext.meeting_attendance.AddRangeAsync(attendances);
                    }
                    await _dBContext.SaveChangesAsync();

                    //return _mapper.Map<GetMeeting>(meeting);
                    return _mapper.Map<GetMeeting>(meeting);

                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Meeting creation failed. Everything was rolled back safely. {ex}", ex);

            }
           
            return finalMeetingData;
        }
    
        private MeetingAttendance BuildAttendance(
            Guid hosterId,Guid meetingId,string participantType,Guid participantId)
        {
            return new MeetingAttendance
            {
                hoster_id = hosterId,
                meeting_id = meetingId,
                participant_type = participantType,
                participant_id = participantId,
                participant_role = null,
                invite_status = "Pending",
                attendance_status = "Pending",
                response_date = null,
                remark = null,
                created_by = _loginContext.userId,
                created_at = DateTime.UtcNow
            };
        }

    }
}
