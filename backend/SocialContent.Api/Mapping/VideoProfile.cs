using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class VideoProfile : Profile
{
    public VideoProfile()
    {
        CreateMap<Video, VideoDto>()
            .ForCtorParam(nameof(VideoDto.FileName), o => o.MapFrom(s => s.FileName.Value));
    }
}
