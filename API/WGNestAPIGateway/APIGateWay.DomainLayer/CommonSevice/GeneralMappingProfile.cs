using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public class GeneralMappingProfile : Profile
    {
        public GeneralMappingProfile()
        {
            CreateMap<ProjectDto, ProjectMaster>().ApplyDynamicIgnores();
            CreateMap<PostTicketDto, TicketMaster>().ApplyDynamicIgnores();
            CreateMap<PostThreadsDto, ThreadMaster>().ApplyDynamicIgnores();
            CreateMap<CreateLabelDto, LabelMaster>().ApplyDynamicIgnores();
            CreateMap<PostMeetingDto, MeetingMaster>().ApplyDynamicIgnores();
            CreateMap<CreateNotificationRequest, NotificationMaster>();

            CreateMap<NotificationAudienceDto, NotificationAudience>();
            CreateMap<ProjectMaster, GetProject>()
                .ForMember(dest => dest.Project_Name, opt => opt.MapFrom(src => src.Title));
           
            CreateMap<TicketMaster, GetTickets>()
                .ForMember(dest => dest.Issue_Id, opt => opt.MapFrom(src => src.Issue_Id));

            CreateMap<ThreadMaster, ThreadList>()
                .ForMember(dest => dest.ThreadId, opt => opt.MapFrom(src => src.ThreadId));
            CreateMap<LabelMaster, GetLabel>();
            CreateMap<PostBannerMessageDto, BannerMessageMaster>();
            CreateMap<PutBannerMessageDto, BannerMessageMaster>();
            CreateMap<BannerMessageMaster, GetBannerMessageSP>();
            CreateMap<MeetingMaster, GetMeetingDto>();

            CreateMap<PostLeaveRequestDto, LeaveRequestMaster>()
                .ForMember(dest => dest.LEAVE_FROM, opt => opt.MapFrom(src => src.LeaveFrom))
                .ForMember(dest => dest.LEAVE_TO, opt => opt.MapFrom(src => src.LeaveTo))
                .ForMember(dest => dest.LEAVE_TYPE_ID, opt => opt.MapFrom(src => src.LeaveTypeId))
                .ForMember(dest => dest.COMMENTS, opt => opt.MapFrom(src => src.Comments));

            CreateMap<LeaveRequestMaster, GetLeaveRequest>()
                .ForMember(dest => dest.ID, opt => opt.MapFrom(src => src.ID))
                .ForMember(dest => dest.EMPLOYEE_ID, opt => opt.MapFrom(src => src.EMPLOYEE_ID))
                .ForMember(dest => dest.EmployeeName, opt => opt.Ignore())
                .ForMember(dest => dest.LEAVE_FROM, opt => opt.MapFrom(src => src.LEAVE_FROM))
                .ForMember(dest => dest.LEAVE_TO, opt => opt.MapFrom(src => src.LEAVE_TO))
                .ForMember(dest => dest.LEAVE_TYPE_ID, opt => opt.MapFrom(src => src.LEAVE_TYPE_ID))
                .ForMember(dest => dest.NO_OF_LEAVE_DAYS, opt => opt.MapFrom(src => src.NO_OF_LEAVE_DAYS))
                .ForMember(dest => dest.COMMENTS, opt => opt.MapFrom(src => src.COMMENTS))
                .ForMember(dest => dest.STATUS, opt => opt.MapFrom(src => src.STATUS))
                .ForMember(dest => dest.REQUESTED_DATE, opt => opt.MapFrom(src => src.REQUESTED_DATE))
                .ForMember(dest => dest.APPROVED_BY, opt => opt.MapFrom(src => src.APPROVED_BY))
                .ForMember(dest => dest.APPROVED_DATE, opt => opt.MapFrom(src => src.APPROVED_DATE))
                .ForMember(dest => dest.REJECT_REASON, opt => opt.MapFrom(src => src.REJECT_REASON))
                .ForMember(dest => dest.REJECTED_BY, opt => opt.MapFrom(src => src.REJECTED_BY))
                .ForMember(dest => dest.REJECTED_DATE, opt => opt.MapFrom(src => src.REJECTED_DATE))
                .ForMember(dest => dest.CREATED_BY, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CREATED_DATE, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.UPDATED_BY, opt => opt.MapFrom(src => src.UpdatedBy))
                .ForMember(dest => dest.UPDATED_DATE, opt => opt.MapFrom(src => src.UpdatedAt))
                .ForMember(dest => dest.NOT_TAKEN, opt => opt.MapFrom(src => src.NOT_TAKEN))
                .ForMember(dest => dest.NOT_TAKEN_BY, opt => opt.MapFrom(src => src.NOT_TAKEN_BY))
                .ForMember(dest => dest.NOT_TAKEN_DATE, opt => opt.MapFrom(src => src.NOT_TAKEN_DATE));

            CreateMap<PostPermissionRequestDto, PermissionRequestMaster>()
                .ForMember(dest => dest.PERMISSION_DATE, opt => opt.MapFrom(src => src.PermissionDate))
                .ForMember(dest => dest.DURATION_MINUTES, opt => opt.MapFrom(src => src.DurationMinutes))
                .ForMember(dest => dest.REMARKS, opt => opt.MapFrom(src => src.Remarks));

            CreateMap<PermissionRequestMaster, GetPermissionRequest>()
                .ForMember(dest => dest.ID, opt => opt.MapFrom(src => src.ID))
                .ForMember(dest => dest.EMPLOYEE_ID, opt => opt.MapFrom(src => src.EMPLOYEE_ID))
                .ForMember(dest => dest.EmployeeName, opt => opt.Ignore())
                .ForMember(dest => dest.PERMISSION_DATE, opt => opt.MapFrom(src => src.PERMISSION_DATE))
                .ForMember(dest => dest.DURATION_MINUTES, opt => opt.MapFrom(src => src.DURATION_MINUTES))
                .ForMember(dest => dest.REMARKS, opt => opt.MapFrom(src => src.REMARKS))
                .ForMember(dest => dest.STATUS, opt => opt.MapFrom(src => src.STATUS))
                .ForMember(dest => dest.REQUESTED_DATE, opt => opt.MapFrom(src => src.REQUESTED_DATE))
                .ForMember(dest => dest.APPROVED_BY, opt => opt.MapFrom(src => src.APPROVED_BY))
                .ForMember(dest => dest.APPROVED_DATE, opt => opt.MapFrom(src => src.APPROVED_DATE))
                .ForMember(dest => dest.REJECT_REASON, opt => opt.MapFrom(src => src.REJECT_REASON))
                .ForMember(dest => dest.REJECTED_BY, opt => opt.MapFrom(src => src.REJECTED_BY))
                .ForMember(dest => dest.REJECTED_DATE, opt => opt.MapFrom(src => src.REJECTED_DATE))
                .ForMember(dest => dest.CREATED_BY, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CREATED_DATE, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.UPDATED_BY, opt => opt.MapFrom(src => src.UpdatedBy))
                .ForMember(dest => dest.UPDATED_DATE, opt => opt.MapFrom(src => src.UpdatedAt))
                .ForMember(dest => dest.ACTUAL_DURATION_MINUTES, opt => opt.MapFrom(src => src.ACTUAL_DURATION_MINUTES))
                .ForMember(dest => dest.ACTUAL_DURATION_BY, opt => opt.MapFrom(src => src.ACTUAL_DURATION_BY))
                .ForMember(dest => dest.ACTUAL_DURATION_DATE, opt => opt.MapFrom(src => src.ACTUAL_DURATION_DATE));
        }
    }
}