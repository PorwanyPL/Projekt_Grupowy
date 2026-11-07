using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class InstagramPostProfile : Profile
{
    public InstagramPostProfile()
    {
        CreateMap<InstagramPost, InstagramPostDetailsDto>()
            .ForMember(d => d.InstagramAccountId, o => o.MapFrom(s => s.InstagramAccountId.Value))
            .ForMember(d => d.LocationId, o => o.MapFrom(s => s.LocationId == null ? null : s.LocationId.Value));
    }
}
