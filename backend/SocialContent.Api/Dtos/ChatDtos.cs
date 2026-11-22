using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Wiadomość czatu. Ten sam kształt przychodzi z REST (historia) i z huba SignalR (zdarzenie ReceiveMessage).</summary>
public record ChatMessageDto(Guid Id, Guid ConversationId, Guid SenderId, UserRole SenderRole, string SenderName,
    string Text, DateTime SentAt);

/// <summary>Uczestnik rozmowy.</summary>
public record ChatParticipantDto(Guid UserId, UserRole Role, string Name);

/// <summary>Rozmowa na liście: tytuł (dla rozmowy 1 na 1 - nazwa rozmówcy), ostatnia wiadomość i liczba nieprzeczytanych.</summary>
public record ConversationDto(Guid Id, ConversationKind Kind, string Title, List<ChatParticipantDto> Participants,
    ChatMessageDto? LastMessage, int UnreadCount);

/// <summary>Osoba, z którą zalogowany użytkownik może rozpocząć rozmowę 1 na 1.</summary>
public record ChatContactDto(Guid UserId, UserRole Role, string Name, string? Description, string? Email = null);

/// <summary>Manager upoważniony do czatowania ze wszystkimi klientami (null = nikt).</summary>
public record ChatManagerDto(Guid? ManagerId, string? ManagerName);

public class OpenDirectConversationDto
{
    /// <summary>Identyfikator rozmówcy (CompanyId, Id managera albo Id klienta).</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Rola rozmówcy: CompanyUser, ManagerUser albo ClientUser.</summary>
    [Required]
    public UserRole Role { get; set; }
}

public class CreateGroupDto
{
    /// <summary>Nazwa grupy (1-100 znaków).</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    /// <summary>Managerowie firmy dodawani do grupy. Właściciel firmy i autor grupy dołączają automatycznie.</summary>
    public List<Guid> ManagerIds { get; set; } = new();
}

public class SendChatMessageDto
{
    /// <summary>Treść wiadomości (1-2000 znaków).</summary>
    [Required, MaxLength(2000)]
    public string Text { get; set; } = "";
}

public class SetChatManagerDto
{
    /// <summary>Id managera firmy albo null, aby odebrać uprawnienie.</summary>
    public Guid? ManagerId { get; set; }
}
