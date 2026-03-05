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

            CreateMap<ProjectMaster, GetProject>()
                .ForMember(dest => dest.Project_Name, opt => opt.MapFrom(src => src.Title));
           
            CreateMap<TicketMaster, GetTickets>()
                .ForMember(dest => dest.Issue_Id, opt => opt.MapFrom(src => src.Issue_Id));

            CreateMap<ThreadMaster, ThreadList>()
                .ForMember(dest => dest.ThreadId, opt => opt.MapFrom(src => src.ThreadId));
            CreateMap<LabelMaster, GetLabel>();
        }
    }
}