namespace SocialContent.Api.Dtos;

/// <summary>Pełna lista managerów przypisanych do konta społecznościowego (pominięci zostaną odpięci).</summary>
public class AssignManagersDto
{
    public List<Guid> ManagerUserIds { get; set; } = new();
}
