using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SocialContent.Api.Controllers;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Hubs;

/// <summary>
/// Hub czatu i powiadomień (/hubs/chat). Wymaga JWT (przeglądarka i klienci bez nagłówków przekazują token w ?access_token=).
/// Serwer -> klient: <c>ReceiveMessage</c> (ChatMessageDto) i <c>ReceiveNotification</c> (NotificationDto). Klient -> serwer:
/// <c>SendMessage(conversationId, text)</c>. Uprawnienia do rozmowy sprawdza serwis czatu; naruszenie kończy się HubException
/// z komunikatem po polsku (połączenie zostaje).
/// </summary>
[Authorize(Roles = Roles.Any)]
public class ChatHub(IChatService chat) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User!.ToCurrentUser();
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroups.Of(user.Id, user.Role));
        await base.OnConnectedAsync();
    }

    /// <summary>Wysyła wiadomość do rozmowy; zwraca zapisaną wiadomość (nadawca dostaje też zdarzenie ReceiveMessage).</summary>
    public async Task<ChatMessageDto> SendMessage(Guid conversationId, string text)
    {
        try
        {
            return await chat.SendAsync(Context.User!.ToCurrentUser(), conversationId, text, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is ForbiddenException or NotFoundException or DomainValidationException or UnauthorizedException)
        {
            throw new HubException(ex.Message);
        }
    }
}
