using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class PostProfile : Profile
{
    public PostProfile()
    {
        // Mapowanie wywołujemy jako Map<Post, PostDto>, a blok platformy (facebook/instagram/tikTok) wybiera typ runtime posta.
        CreateMap<Post, PostDto>()
            .ForCtorParam(nameof(PostDto.Title), o => o.MapFrom(s => s.Title.Value))
            .ForCtorParam(nameof(PostDto.CreatedAt), o => o.MapFrom(s => DateTime.SpecifyKind(s.CreatedAt.Date, DateTimeKind.Utc)))
            .ForCtorParam(nameof(PostDto.Description), o => o.MapFrom(s => s.Description.Value))
            .ForCtorParam(nameof(PostDto.Pictures), o => o.MapFrom(s => s.Pictures))
            .ForCtorParam(nameof(PostDto.Videos), o => o.MapFrom(s => s.Videos))
            .ForCtorParam(nameof(PostDto.ScheduledAt), o => o.MapFrom(s =>
                s.ScheduledAt == null ? (DateTime?)null : DateTime.SpecifyKind(s.ScheduledAt.Date, DateTimeKind.Utc)))
            .ForCtorParam(nameof(PostDto.PublishedAt), o => o.MapFrom(s =>
                s.PublishedAt == null ? (DateTime?)null : DateTime.SpecifyKind(s.PublishedAt.Date, DateTimeKind.Utc)))
            .ForCtorParam(nameof(PostDto.ExternalPostId), o => o.MapFrom(s => s.ExternalPostId == null ? null : s.ExternalPostId.Value))
            .ForCtorParam(nameof(PostDto.Facebook), o => o.MapFrom(s => s as FacebookPost))
            .ForCtorParam(nameof(PostDto.Instagram), o => o.MapFrom(s => s as InstagramPost))
            .ForCtorParam(nameof(PostDto.TikTok), o => o.MapFrom(s => s as TikTokPost))
            // Flagi zależne od zalogowanego użytkownika i blokada edycji ustawia PostService (with).
            .ForCtorParam(nameof(PostDto.CanEdit), o => o.MapFrom(_ => false))
            .ForCtorParam(nameof(PostDto.CanDelete), o => o.MapFrom(_ => false))
            .ForCtorParam(nameof(PostDto.Lock), o => o.MapFrom(_ => (PostLockDto?)null));
    }
}
