using AutoMapper;
using AutoMapper.Configuration;
using StickyFlow.Dtos;
using StickyFlow.Models;

namespace StickyFlow.Profiles
{
    public class MappingProfiles : Profile
    {
        public MappingProfiles()
        {
            CreateMap<Note, NoteDto>()
                .ForMember(dest => dest.PriorityName, opt => opt.MapFrom(src => src.Priority.Name))
                .ForMember(dest => dest.PriorityColorCode, opt => opt.MapFrom(src => src.Priority.Color))
                .ForMember(dest => dest.StatusName, opt => opt.MapFrom(src => src.Status.Name))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));

            CreateMap<NoteCreateDto, Note>()
                .ForMember(dest => dest.NoteId, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.UserId, opt => opt.Ignore()) // Set in controller
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => false));

            CreateMap<NoteUpdateDto, Note>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            
                
        }
    }
}