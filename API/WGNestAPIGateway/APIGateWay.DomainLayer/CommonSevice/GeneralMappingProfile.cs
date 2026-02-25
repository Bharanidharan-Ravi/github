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

            CreateMap<ProjectMaster, GetProject>()
                .ForMember(dest => dest.Project_Name, opt => opt.MapFrom(src => src.Title));
        }
    }
}