using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>
/// Czat (REST): lista rozmów i kontaktów, historia, wysyłanie, grupy firmy oraz wskazanie managera ds. czatu.
/// Wiadomości na żywo dostarcza hub SignalR <c>/hubs/chat</c> (zdarzenia <c>ReceiveMessage</c>, <c>ReceiveNotification</c>;
/// metoda <c>SendMessage</c>). Reguły uprawnień wymusza serwer przy każdym wywołaniu: zabronione operacje kończą się kodem 403.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Any)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[Route("api/[controller]")]
public class ChatController(IChatService service) : ControllerBase
{
    /// <summary>Kontakty: z kim mogę rozpocząć rozmowę 1 na 1</summary>
    /// <remarks>
    /// Firma: managerowie własnej firmy, inne firmy i wszyscy klienci. Manager: właściciel i managerowie własnej firmy oraz klienci
    /// współpracujący z firmą (manager ds. czatu - wszyscy klienci). Klient: firmy oraz managerowie, którzy mogą z nim czatować.
    /// Parametr <c>search</c> filtruje listę po stronie serwera, już po zastosowaniu reguł czatu, więc nie da się znaleźć osoby, z którą nie wolno pisać.
    /// Tryby <c>mode</c>: <c>Prefix</c> (nazwa lub jej człon zaczyna się od frazy), <c>Contains</c> (fraza w dowolnym miejscu nazwy, domyślny),
    /// <c>Email</c> (fraza w adresie e-mail). Wielkość liter nie ma znaczenia.
    /// </remarks>
    /// <param name="search">Fraza wyszukiwania (do 100 znaków); pusta = wszystkie dozwolone kontakty.</param>
    /// <param name="mode">Sposób dopasowania frazy.</param>
    /// <response code="200">Lista kontaktów (dopasowanych do frazy).</response>
    [HttpGet("contacts")]
    [ProducesResponseType<List<ChatContactDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatContactDto>>> Contacts(
        [FromQuery] string? search, [FromQuery] ChatContactSearchMode mode, CancellationToken ct) =>
        Ok(await service.SearchContactsAsync(User.ToCurrentUser(), search, mode, ct));

    /// <summary>Moje rozmowy (1 na 1 i grupy)</summary>
    /// <remarks>Od ostatnio aktywnej; zawiera ostatnią wiadomość i liczbę nieprzeczytanych.</remarks>
    /// <response code="200">Lista rozmów.</response>
    [HttpGet("conversations")]
    [ProducesResponseType<List<ConversationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ConversationDto>>> Conversations(CancellationToken ct) =>
        Ok(await service.GetConversationsAsync(User.ToCurrentUser(), ct));

    /// <summary>Otwórz rozmowę 1 na 1 (utworzy ją, jeśli jeszcze nie istnieje)</summary>
    /// <response code="200">Rozmowa.</response>
    /// <response code="403">Reguły czatu nie pozwalają na rozmowę z tym użytkownikiem.</response>
    [HttpPost("conversations/direct")]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConversationDto>> OpenDirect(OpenDirectConversationDto dto, CancellationToken ct) =>
        Ok(await service.OpenDirectAsync(User.ToCurrentUser(), dto.UserId, dto.Role, ct));

    /// <summary>Utwórz grupę w obrębie firmy</summary>
    /// <remarks>Tylko firma i jej managerowie. W grupie są: właściciel firmy, autor i wskazani managerowie tej firmy.</remarks>
    /// <response code="200">Utworzona grupa.</response>
    /// <response code="400">Niepoprawna nazwa albo manager spoza firmy.</response>
    /// <response code="403">Klient nie tworzy grup.</response>
    [HttpPost("groups")]
    [Authorize(Roles = Roles.CompanyOrManager)]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConversationDto>> CreateGroup(CreateGroupDto dto, CancellationToken ct) =>
        Ok(await service.CreateGroupAsync(User.ToCurrentUser(), dto, ct));

    /// <summary>Historia rozmowy</summary>
    /// <param name="id">Identyfikator rozmowy.</param>
    /// <param name="before">Tylko wiadomości starsze niż ta data (UTC) - ładowanie wcześniejszych stron.</param>
    /// <param name="limit">Liczba wiadomości (domyślnie 50, maksymalnie 200).</param>
    /// <response code="200">Wiadomości od najstarszej do najnowszej.</response>
    /// <response code="403">Rozmowa jest spoza dozwolonej pary lub grupy.</response>
    /// <response code="404">Rozmowa nie istnieje.</response>
    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType<List<ChatMessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ChatMessageDto>>> Messages(Guid id, [FromQuery] DateTime? before,
        [FromQuery] int limit = 50, CancellationToken ct = default) =>
        Ok(await service.GetMessagesAsync(User.ToCurrentUser(), id, before, limit, ct));

    /// <summary>Wyślij wiadomość (REST; klient aplikacji używa metody huba SendMessage)</summary>
    /// <remarks>Wiadomość jest zapisywana i dostarczana na żywo wszystkim uczestnikom.</remarks>
    /// <response code="200">Zapisana wiadomość.</response>
    /// <response code="400">Pusta lub za długa wiadomość.</response>
    /// <response code="403">Rozmowa jest spoza dozwolonej pary lub grupy.</response>
    [HttpPost("conversations/{id:guid}/messages")]
    [ProducesResponseType<ChatMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChatMessageDto>> Send(Guid id, SendChatMessageDto dto, CancellationToken ct) =>
        Ok(await service.SendAsync(User.ToCurrentUser(), id, dto.Text, ct));

    /// <summary>Oznacz rozmowę jako przeczytaną</summary>
    /// <response code="204">Oznaczono.</response>
    [HttpPost("conversations/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(User.ToCurrentUser(), id, ct);
        return NoContent();
    }

    /// <summary>Manager upoważniony do czatowania ze wszystkimi klientami</summary>
    /// <response code="200">Wybrany manager (albo brak).</response>
    /// <response code="403">Tylko właściciel firmy.</response>
    [HttpGet("chat-manager")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<ChatManagerDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatManagerDto>> GetChatManager(CancellationToken ct) =>
        Ok(await service.GetChatManagerAsync(User.ToCurrentUser(), ct));

    /// <summary>Wyznacz albo zmień managera ds. czatu</summary>
    /// <remarks>
    /// Dokładnie jeden manager firmy może czatować ze wszystkimi klientami, także tymi, którzy nie współpracują z firmą.
    /// Przekaż <c>managerId: null</c>, aby odebrać uprawnienie.
    /// </remarks>
    /// <response code="200">Aktualny manager ds. czatu.</response>
    /// <response code="400">Manager nie istnieje, jest zablokowany albo należy do innej firmy.</response>
    [HttpPut("chat-manager")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<ChatManagerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatManagerDto>> SetChatManager(SetChatManagerDto dto, CancellationToken ct) =>
        Ok(await service.SetChatManagerAsync(User.ToCurrentUser(), dto.ManagerId, ct));
}
