using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

/// <summary>Profil zalogowanego managera: e-mail pochodzi z konta logowania, reszta z powiązanego ManagerUser.</summary>
public class ManagerCredentialProfile : Profile
{
    public ManagerCredentialProfile()
    {
        CreateMap<ManagerCredential, UserProfileDto>()
            .ForCtorParam(nameof(UserProfileDto.Id), o => o.MapFrom(s => s.ManagerUserId))
            .ForCtorParam(nameof(UserProfileDto.Role), o => o.MapFrom(_ => UserRole.ManagerUser.ToString()))
            .ForCtorParam(nameof(UserProfileDto.DisplayName),
                o => o.MapFrom(s => s.ManagerUser.FirstName.Value + " " + s.ManagerUser.LastName.Value))
            .ForCtorParam(nameof(UserProfileDto.CompanyId), o => o.MapFrom(s => (Guid?)s.ManagerUser.CompanyId))
            .ForCtorParam(nameof(UserProfileDto.Age), o => o.MapFrom(s => (int?)s.ManagerUser.Age.Value))
            .ForCtorParam(nameof(UserProfileDto.Description), o => o.MapFrom(s => s.ManagerUser.Description.Value));
    }
}
