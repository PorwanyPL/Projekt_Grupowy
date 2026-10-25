using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Zarządzanie postami do mediów społecznościowych: ogólnymi oraz dla Facebooka, Instagrama i TikToka.</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[Route("api/[controller]")]
public class PostsController(IPostService service) : ControllerBase
{
    /// <summary>Pobierz listę postów</summary>
    /// <remarks>
    /// <para>Zwraca posty od najnowszego (createdAt malejąco) wraz ze zdjęciami, wideo i przypisanymi managerami. Wymaga tokenu JWT (nagłówek Authorization: Bearer).</para>
    /// <para>Opcjonalny parametr type zawęża wynik do postów o danym polu visibility (Text, Photo, Video). Pole platform wskazuje rodzaj posta (Facebook, Instagram, TikTok; null dla posta ogólnego), a odpowiedni blok facebook, instagram lub tikTok zawiera dane platformy.</para>
    /// </remarks>
    /// <param name="type">Filtr po typie treści (visibility); pominięty = wszystkie posty.</param>
    /// <response code="200">Lista postów; pusta tablica, gdy nic nie pasuje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet]
    [ProducesResponseType<List<PostDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PostDto>>> GetAll([FromQuery] FacebookPostType? type, CancellationToken ct) =>
        Ok(await service.GetAllAsync(type, ct));

    /// <summary>Pobierz post po identyfikatorze</summary>
    /// <remarks>Zwraca jeden post ze wszystkimi polami: statusem publikacji, terminami, kontem docelowym, mediami, managerami i danymi platformy.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="200">Znaleziony post.</response>
    /// <response code="404">Post o podanym identyfikatorze nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Utwórz post</summary>
    /// <remarks>
    /// <para>Tworzy post ogólny albo post platformy. Aby utworzyć post platformy, wypełnij dokładnie jeden blok: facebook, instagram lub tikTok. Bez bloku powstaje post ogólny. Rodzaju posta nie da się później zmienić.</para>
    /// <para>Reguły mediów zależą od pola visibility: Text nie może mieć zdjęć ani wideo, Photo wymaga co najmniej jednego zdjęcia i nie dopuszcza wideo, Video wymaga co najmniej jednego wideo. Przesyłane są tylko metadane plików (nazwa, format, rozmiar w KB lub długość w sekundach).</para>
    /// <para>Status ustawia serwer: bez scheduledAt post jest szkicem (Draft), z terminem w przyszłości (UTC) - zaplanowanym (Scheduled). Pole createdAt również nadaje serwer.</para>
    /// <para>Opcjonalne targetAccountId wskazuje konto docelowe; dla posta platformy musi być to konto tej samej platformy. Managerowie przypisywani są listą managerUserIds.</para>
    /// </remarks>
    /// <param name="dto">Dane nowego posta.</param>
    /// <response code="201">Post został utworzony; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (m.in. media niezgodne z visibility, termin z przeszłości, kilka bloków platform naraz, konto innej platformy).</response>
    /// <response code="404">Wskazany manager lub konto docelowe nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPost]
    [ProducesResponseType<PostDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDto>> Create(PostWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Zaktualizuj post</summary>
    /// <remarks>
    /// <para>Pełna aktualizacja: zastępuje treść, media, managerów, termin i konto docelowe wartościami z żądania; dotychczasowe media są usuwane i zastępowane nowymi.</para>
    /// <para>Żądanie musi mieć blok platformy zgodny z rodzajem posta (post Facebooka - blok facebook, i tak dalej; post ogólny - żadnego). Tych samych reguł mediów, terminu i konta co przy tworzeniu pilnuje walidacja.</para>
    /// <para>Status jest wyliczany od nowa: bez scheduledAt - Draft, z terminem - Scheduled; ewentualny komunikat błędu po nieudanej publikacji jest czyszczony. Posta w statusie Publishing lub Published nie można edytować.</para>
    /// </remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <param name="dto">Nowe dane posta.</param>
    /// <response code="200">Post po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji, blok platformy nie pasuje do posta albo post ma status Publishing lub Published.</response>
    /// <response code="404">Post, wskazany manager lub konto docelowe nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDto>> Update(Guid id, PostWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    /// <summary>Usuń post</summary>
    /// <remarks>Trwale usuwa post razem z jego zdjęciami i wideo oraz powiązaniami z managerami. Managerowie i konta społecznościowe pozostają. Operacji nie można cofnąć.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="204">Post został usunięty.</response>
    /// <response code="404">Post o podanym identyfikatorze nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
