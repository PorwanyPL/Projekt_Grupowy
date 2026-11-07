using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class FacebookPostProfile : Profile
{
    public FacebookPostProfile()
    {
        CreateMap<FacebookPost, FacebookPostDetailsDto>()
            .ForMember(d => d.TargetPageId, o => o.MapFrom(s => s.TargetPageId.Value))
            .ForMember(d => d.LinkUrl, o => o.MapFrom(s => s.LinkUrl == null ? null : s.LinkUrl.Url));
    }
}
