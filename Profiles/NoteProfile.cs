using AutoMapper;
using StickyFlow.Dtos;
using StickyFlow.Models;

namespace StickyFlow.Profiles
{
    public class NoteProfile : Profile
    {
        public NoteProfile()
        {
            CreateMap<Note, NoteDto>()
                .ForMember(dest => dest.PriorityName, opt => opt.MapFrom(src => src.Priority.Name))
                .ForMember(dest => dest.PriorityColorCode, opt => opt.MapFrom(src => src.Priority.Color))
                .ForMember(dest => dest.StatusName, opt => opt.MapFrom(src => src.Status.Name))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));

            CreateMap<NoteCreateDto, Note>();
            CreateMap<NoteUpdateDto, Note>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}
