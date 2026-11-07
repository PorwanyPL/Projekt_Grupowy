using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Powiadomienia zalogowanego użytkownika (np. o nieudanej publikacji posta).</summary>
[ApiController]
[Authorize(Roles = Roles.CompanyOrManager)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[Route("api/[controller]")]
public class NotificationsController(INotificationService service) : ControllerBase
{
    /// <summary>Pobierz swoje powiadomienia</summary>
    /// <remarks>Od najnowszych. Powiadomienia dostają firma i autorzy posta, którego publikacja się nie udała.</remarks>
    /// <response code="200">Lista powiadomień.</response>
    [HttpGet]
    [ProducesResponseType<List<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NotificationDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAsync(User.ToCurrentUser(), ct));

    /// <summary>Oznacz powiadomienie jako przeczytane</summary>
    /// <param name="id">Identyfikator powiadomienia.</param>
    /// <response code="204">Oznaczono.</response>
    /// <response code="404">Powiadomienie nie istnieje.</response>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(User.ToCurrentUser(), id, ct);
        return NoContent();
    }

    /// <summary>Oznacz wszystkie powiadomienia jako przeczytane</summary>
    /// <response code="204">Oznaczono.</response>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await service.MarkAllReadAsync(User.ToCurrentUser(), ct);
        return NoContent();
    }
}
