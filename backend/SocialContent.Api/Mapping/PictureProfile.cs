using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class PictureProfile : Profile
{
    public PictureProfile()
    {
        CreateMap<Picture, PictureDto>()
            .ForCtorParam(nameof(PictureDto.FileName), o => o.MapFrom(s => s.FileName.Value));
    }
}
