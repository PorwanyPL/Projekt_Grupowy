using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class ManagerUserProfile : Profile
{
    public ManagerUserProfile()
    {
        // Pola SocialAccountIds, HasLoginAccount, IsAccessBlocked i Email są pomijane (null): wypełnia je tylko ManagerUserService,
        // bo wymagają danych spoza encji (konto logowania, przypisane konta).
        CreateMap<ManagerUser, ManagerUserDto>()
            .ForCtorParam(nameof(ManagerUserDto.FirstName), o => o.MapFrom(s => s.FirstName.Value))
            .ForCtorParam(nameof(ManagerUserDto.LastName), o => o.MapFrom(s => s.LastName.Value))
            .ForCtorParam(nameof(ManagerUserDto.Age), o => o.MapFrom(s => s.Age.Value))
            .ForCtorParam(nameof(ManagerUserDto.Description), o => o.MapFrom(s => s.Description.Value))
            .ForCtorParam(nameof(ManagerUserDto.SocialAccountIds), o => o.MapFrom(_ => (List<Guid>?)null))
            .ForCtorParam(nameof(ManagerUserDto.HasLoginAccount), o => o.MapFrom(_ => (bool?)null))
            .ForCtorParam(nameof(ManagerUserDto.IsAccessBlocked), o => o.MapFrom(_ => (bool?)null))
            .ForCtorParam(nameof(ManagerUserDto.Email), o => o.MapFrom(_ => (string?)null));
    }
}
