using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class TikTokPostProfile : Profile
{
    public TikTokPostProfile()
    {
        CreateMap<TikTokPost, TikTokPostDetailsDto>()
            .ForMember(d => d.TikTokAccountId, o => o.MapFrom(s => s.TikTokAccountId.Value));
    }
}
