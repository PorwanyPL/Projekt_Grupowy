using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class PostEditLockProfile : Profile
{
    public PostEditLockProfile()
    {
        // HeldByMe zależy od zalogowanego użytkownika, więc ustawia je serwis (with).
        CreateMap<PostEditLock, PostLockDto>()
            .ForCtorParam(nameof(PostLockDto.ExpiresAt), o => o.MapFrom(s => DateTime.SpecifyKind(s.ExpiresAt, DateTimeKind.Utc)))
            .ForCtorParam(nameof(PostLockDto.HeldByMe), o => o.MapFrom(_ => false));
    }
}
