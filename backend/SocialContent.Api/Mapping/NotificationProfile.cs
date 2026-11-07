using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class NotificationProfile : Profile
{
    public NotificationProfile()
    {
        CreateMap<Notification, NotificationDto>()
            .ForCtorParam(nameof(NotificationDto.CreatedAt), o => o.MapFrom(s => DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc)));
    }
}
