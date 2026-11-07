using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Posty do mediów społecznościowych. Odczyt: firma (wszystkie posty) i manager (tylko własne); zapis: CompanyUser (każde konto firmy) i ManagerUser przypisany do konta.</summary>
[ApiController]
[Authorize(Roles = Roles.CompanyOrManager)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public class PostsController(IPostService service) : ControllerBase
{
    /// <summary>Pobierz listę postów</summary>
    /// <remarks>
    /// <para>Zwraca posty od najnowszego (createdAt malejąco) wraz ze zdjęciami, wideo i autorami. CompanyUser widzi posty całej firmy, ManagerUser - wyłącznie posty, których jest autorem. Wymaga tokenu JWT (nagłówek Authorization: Bearer).</para>
    /// <para>Parametry accountId (konto docelowe) i managerId (autor) zawężają wynik; manager może podać wyłącznie własny identyfikator (inny kończy się odpowiedzią 403). Opcjonalny parametr type zawęża wynik do postów o danym polu visibility (Text, Photo, Video). Pole platform wskazuje rodzaj posta (Facebook, Instagram, TikTok; null dla posta ogólnego), a odpowiedni blok facebook, instagram lub tikTok zawiera dane platformy.</para>
    /// </remarks>
    /// <param name="type">Filtr po typie treści (visibility); pominięty = wszystkie posty.</param>
    /// <param name="accountId">Filtr po koncie społecznościowym (id w systemie).</param>
    /// <param name="managerId">Filtr po autorze (id managera); używane przez firmę do podglądu postów danego managera.</param>
    /// <response code="200">Lista postów; pusta tablica, gdy nic nie pasuje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Manager próbuje przeglądać posty innego managera.</response>
    [HttpGet]
    [ProducesResponseType<List<PostDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PostDto>>> GetAll(
        [FromQuery] FacebookPostType? type, [FromQuery] Guid? accountId, [FromQuery] Guid? managerId, CancellationToken ct) =>
        Ok(await service.GetAllAsync(User.ToCurrentUser(), type, accountId, managerId, ct));

    /// <summary>Pobierz post po identyfikatorze</summary>
    /// <remarks>Zwraca jeden post ze wszystkimi polami: statusem publikacji, terminami, kontem docelowym, mediami, managerami i danymi platformy.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="200">Znaleziony post.</response>
    /// <response code="404">Post nie istnieje albo nie jest widoczny dla zalogowanego użytkownika.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(User.ToCurrentUser(), id, ct));

    /// <summary>Utwórz post</summary>
    /// <remarks>
    /// <para>Tworzy post ogólny albo post platformy. Aby utworzyć post platformy, wypełnij dokładnie jeden blok: facebook, instagram lub tikTok. Bez bloku powstaje post ogólny. Rodzaju posta nie da się później zmienić.</para>
    /// <para>Reguły mediów zależą od pola visibility: Text nie może mieć zdjęć ani wideo, Photo wymaga co najmniej jednego zdjęcia i nie dopuszcza wideo, Video wymaga co najmniej jednego wideo. Pliki najpierw przesyła się przez POST /api/Media (multipart), a w poście podaje się zwrócony storageKey i nazwę pliku; format i rozmiar ustala serwer.</para>
    /// <para>Status ustawia serwer: bez scheduledAt post jest szkicem (Draft), z terminem w przyszłości (UTC) - zaplanowanym (Scheduled), a z publishNow=true publikowany jest od razu (Published albo Failed z komunikatem; o błędzie dostają powiadomienie firma i autorzy). Publikacja jest symulowana. Pole createdAt nadaje serwer.</para>
    /// <para>CompanyUser tworzy posty na każdym koncie swojej firmy (bez względu na przypisanych managerów), ManagerUser tylko na koncie, do którego jest przypisany. targetAccountId jest wymagane; dla posta platformy musi to być konto tej samej platformy. Autorami są od razu wszyscy managerowie przypisani do konta (współautorzy) - lista nie jest podawana w żądaniu.</para>
    /// </remarks>
    /// <param name="dto">Dane nowego posta.</param>
    /// <response code="201">Post został utworzony; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (m.in. media niezgodne z visibility, termin z przeszłości, kilka bloków platform naraz, konto innej platformy).</response>
    /// <response code="403">Manager nie jest przypisany do konta docelowego albo stracił do niego prawa (wtedy post można tylko przeglądać).</response>
    /// <response code="404">Konto docelowe nie istnieje (lub należy do innej firmy).</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPost]
    [ProducesResponseType<PostDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDto>> Create(PostWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(User.ToCurrentUser(), dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Zaktualizuj post</summary>
    /// <remarks>
    /// <para>Pełna aktualizacja: zastępuje treść, media, termin i konto docelowe wartościami z żądania; dotychczasowe media są usuwane i zastępowane nowymi.</para>
    /// <para>Żądanie musi mieć blok platformy zgodny z rodzajem posta (post Facebooka - blok facebook, i tak dalej; post ogólny - żadnego). Tych samych reguł mediów, terminu i konta co przy tworzeniu pilnuje walidacja.</para>
    /// <para>Status jest wyliczany od nowa: bez scheduledAt - Draft, z terminem - Scheduled; ewentualny komunikat błędu po nieudanej publikacji jest czyszczony. Posta w statusie Publishing lub Published nie można edytować. Zapis wymaga blokady edycji: gdy post edytuje ktoś inny, odpowiedź to 409 z imieniem edytującego. Dotychczasowi autorzy zostają, a managerowie przypisani do konta są dopisywani.</para>
    /// </remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <param name="dto">Nowe dane posta.</param>
    /// <response code="200">Post po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji, blok platformy nie pasuje do posta albo post ma status Publishing lub Published.</response>
    /// <response code="403">Manager nie jest przypisany do konta docelowego albo stracił do niego prawa (wtedy post można tylko przeglądać).</response>
    /// <response code="404">Post (niewidoczny dla użytkownika) lub konto docelowe nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostDto>> Update(Guid id, PostWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(User.ToCurrentUser(), id, dto, ct));

    /// <summary>Usuń post</summary>
    /// <remarks>CompanyUser usuwa posty kont swojej firmy, ManagerUser własne posty na koncie, do którego nadal jest przypisany. Trwale usuwa post razem z jego zdjęciami i wideo oraz powiązaniami z managerami. Managerowie i konta społecznościowe pozostają. Operacji nie można cofnąć.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="204">Post został usunięty.</response>
    /// <response code="403">Manager stracił prawa do konta posta (tylko podgląd).</response>
    /// <response code="404">Post nie istnieje albo nie jest widoczny dla użytkownika.</response>
    /// <response code="409">Post jest właśnie edytowany przez inną osobę.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(User.ToCurrentUser(), id, ct);
        return NoContent();
    }

    /// <summary>Zajmij lub odnów blokadę edycji posta</summary>
    /// <remarks>Blokada wygasa sama po kilku minutach bez odnowienia (klient odnawia ją co ok. minutę). Gdy post edytuje ktoś inny, odpowiedź to 409 z jego imieniem i nazwiskiem.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="200">Blokada należy do wywołującego; zwraca czas wygaśnięcia.</response>
    /// <response code="403">Brak prawa zmiany posta.</response>
    /// <response code="404">Post nie istnieje albo nie jest widoczny.</response>
    /// <response code="409">Post jest edytowany przez inną osobę.</response>
    [HttpPost("{id:guid}/lock")]
    [ProducesResponseType<PostLockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PostLockDto>> AcquireLock(Guid id, CancellationToken ct) =>
        Ok(await service.AcquireLockAsync(User.ToCurrentUser(), id, ct));

    /// <summary>Zwolnij blokadę edycji posta</summary>
    /// <remarks>Zwalnia blokadę tylko jej posiadacz; w innym przypadku nic się nie dzieje.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="204">Blokada zwolniona (lub nie istniała).</response>
    [HttpDelete("{id:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseLock(Guid id, CancellationToken ct)
    {
        await service.ReleaseLockAsync(User.ToCurrentUser(), id, ct);
        return NoContent();
    }
}
