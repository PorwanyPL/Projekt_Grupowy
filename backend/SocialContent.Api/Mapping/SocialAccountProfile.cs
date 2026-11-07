using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class SocialAccountProfile : Profile
{
    public SocialAccountProfile()
    {
        // Tokeny (AccessToken, RefreshToken) nie trafiają do DTO - zostaje tylko informacja, czy jest token odświeżania.
        CreateMap<SocialAccount, SocialAccountDto>()
            .ForCtorParam(nameof(SocialAccountDto.TokenExpiresAt),
                o => o.MapFrom(s => DateTime.SpecifyKind(s.TokenExpiresAt, DateTimeKind.Utc)))
            .ForCtorParam(nameof(SocialAccountDto.HasRefreshToken), o => o.MapFrom(s => !string.IsNullOrEmpty(s.RefreshToken)))
            .ForCtorParam(nameof(SocialAccountDto.Pictures), o => o.MapFrom(s => s.Pictures))
            .ForCtorParam(nameof(SocialAccountDto.Videos), o => o.MapFrom(s => s.Videos));
    }
}
