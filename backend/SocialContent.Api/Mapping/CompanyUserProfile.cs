using AutoMapper;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Mapping;

public class CompanyUserProfile : Profile
{
    public CompanyUserProfile()
    {
        CreateMap<CompanyUser, CompanyDto>();

        CreateMap<CompanyUser, UserProfileDto>()
            .ForCtorParam(nameof(UserProfileDto.Id), o => o.MapFrom(s => s.CompanyId))
            .ForCtorParam(nameof(UserProfileDto.Role), o => o.MapFrom(_ => UserRole.CompanyUser.ToString()))
            .ForCtorParam(nameof(UserProfileDto.DisplayName), o => o.MapFrom(s => s.Name))
            .ForCtorParam(nameof(UserProfileDto.CompanyId), o => o.MapFrom(s => (Guid?)s.CompanyId))
            .ForCtorParam(nameof(UserProfileDto.Age), o => o.MapFrom(_ => (int?)null))
            .ForCtorParam(nameof(UserProfileDto.Description), o => o.MapFrom(_ => (string?)null));
    }
}
