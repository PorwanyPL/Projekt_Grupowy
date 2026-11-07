using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class ClientUserProfile : Profile
{
    public ClientUserProfile()
    {
        CreateMap<ClientUser, ClientUserDto>()
            .ForCtorParam(nameof(ClientUserDto.FirstName), o => o.MapFrom(s => s.FirstName.Value))
            .ForCtorParam(nameof(ClientUserDto.LastName), o => o.MapFrom(s => s.LastName.Value))
            .ForCtorParam(nameof(ClientUserDto.Age), o => o.MapFrom(s => s.Age.Value))
            .ForCtorParam(nameof(ClientUserDto.Description), o => o.MapFrom(s => s.Description.Value));

        CreateMap<ClientUser, UserProfileDto>()
            .ForCtorParam(nameof(UserProfileDto.Role), o => o.MapFrom(_ => UserRole.ClientUser.ToString()))
            .ForCtorParam(nameof(UserProfileDto.DisplayName), o => o.MapFrom(s => s.FirstName.Value + " " + s.LastName.Value))
            .ForCtorParam(nameof(UserProfileDto.CompanyId), o => o.MapFrom(_ => (Guid?)null))
            .ForCtorParam(nameof(UserProfileDto.Age), o => o.MapFrom(s => (int?)s.Age.Value))
            .ForCtorParam(nameof(UserProfileDto.Description), o => o.MapFrom(s => s.Description.Value));
    }
}
