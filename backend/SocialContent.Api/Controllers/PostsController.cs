using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Posty do mediów społecznościowych. Odczyt: firma (wszystkie posty), manager (tylko własne) i klient (posty własnych kont, bez edycji); zapis: CompanyUser (każde konto firmy) i ManagerUser przypisany do konta.</summary>
[ApiController]
[Authorize(Roles = Roles.Any)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public class PostsController(IPostService service) : ControllerBase
{
    /// <summary>Pobierz listę postów (z filtrami)</summary>
    /// <remarks>
    /// <para>Zwraca posty od najnowszego (createdAt malejąco) wraz ze zdjęciami, wideo i autorami. CompanyUser widzi posty całej firmy, ManagerUser - wyłącznie posty, których jest autorem, ClientUser - posty swoich kont (bez edycji). Wymaga tokenu JWT (nagłówek Authorization: Bearer).</para>
    /// <para>Post przeznaczony na kilka platform jest jednym wpisem (id = identyfikator grupy), a jego konta i statusy poszczególnych platform są w polu targets; status zbiorczy to najpilniejszy ze statusów wariantów.</para>
    /// <para>Filtry (parametry zapytania) łączą się koniunkcją, a wartości jednej listy alternatywą: status (powtarzany: status=Draft&amp;status=Failed), managerId (przypisana osoba; manager może podać tylko siebie, inaczej 403), tag (powtarzany), accountIds (profile), platform (powtarzany: Facebook, Instagram, TikTok), from i to (zakres dat: termin publikacji, a bez niego czas publikacji lub data utworzenia; UTC, włącznie), type (visibility: Text, Photo, Video) oraz accountId (post ma wariant na tym koncie). Filtry działają zawsze w zakresie postów widocznych dla roli.</para>
    /// </remarks>
    /// <param name="filter">Filtry listy.</param>
    /// <response code="200">Lista postów; pusta tablica, gdy nic nie pasuje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Manager próbuje przeglądać posty innego managera.</response>
    [HttpGet]
    [ProducesResponseType<List<PostDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PostDto>>> GetAll([FromQuery] PostFilterDto filter, CancellationToken ct) =>
        Ok(await service.GetAllAsync(User.ToCurrentUser(), filter, ct));

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
    /// <para>CompanyUser tworzy posty na każdym koncie swojej firmy (bez względu na przypisanych managerów), ManagerUser tylko na koncie, do którego jest przypisany. Konto podaje się w targetAccountId (jedno) albo targetAccountIds (kilka): jeden post na kilka platform powstaje jako grupa wariantów (po jednym na konto) i w API jest jednym postem. Dla jednego konta blok platformy musi być zgodny z kontem; przy wielu kontach blok dotyczy kont swojej platformy, a pozostałe dostają posty ogólne. Tagi treści (tags) służą do filtrowania. Autorami są od razu wszyscy managerowie przypisani do konta (współautorzy) - lista nie jest podawana w żądaniu.</para>
    /// </remarks>
    /// <param name="dto">Dane nowego posta.</param>
    /// <response code="201">Post został utworzony; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (m.in. media niezgodne z visibility, termin z przeszłości, kilka bloków platform naraz, konto innej platformy).</response>
    /// <response code="403">Manager nie jest przypisany do konta docelowego albo stracił do niego prawa (wtedy post można tylko przeglądać).</response>
    /// <response code="404">Konto docelowe nie istnieje (lub należy do innej firmy).</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPost]
    [Authorize(Roles = Roles.CompanyOrManager)]
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
    /// <para>Dla posta na wiele platform lista targetAccountIds wyznacza docelowe konta: brakujące warianty są dodawane, a te, których nie ma na liście - usuwane. Żądanie musi mieć blok platformy zgodny z rodzajem wariantu (post Facebooka - blok facebook, i tak dalej; post ogólny - żadnego). Tych samych reguł mediów, terminu i konta co przy tworzeniu pilnuje walidacja.</para>
    /// <para>Status jest wyliczany od nowa: bez scheduledAt - Draft, z terminem - Scheduled; ewentualny komunikat błędu po nieudanej publikacji jest czyszczony. Posta w statusie Publishing nie można edytować; opublikowany można (bez ponownej publikacji i terminu). Edycja posta, który był już zaplanowany lub opublikowany, nie wraca do akceptacji klienta. Zapis wymaga blokady edycji: gdy post edytuje ktoś inny, odpowiedź to 409 z imieniem edytującego. Dotychczasowi autorzy zostają, a managerowie przypisani do konta są dopisywani.</para>
    /// </remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <param name="dto">Nowe dane posta.</param>
    /// <response code="200">Post po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji, blok platformy nie pasuje do posta albo post ma status Publishing lub Published.</response>
    /// <response code="403">Manager nie jest przypisany do konta docelowego albo stracił do niego prawa (wtedy post można tylko przeglądać).</response>
    /// <response code="404">Post (niewidoczny dla użytkownika) lub konto docelowe nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.CompanyOrManager)]
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
    [Authorize(Roles = Roles.CompanyOrManager)]
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
    [Authorize(Roles = Roles.CompanyOrManager)]
    [ProducesResponseType<PostLockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PostLockDto>> AcquireLock(Guid id, CancellationToken ct) =>
        Ok(await service.AcquireLockAsync(User.ToCurrentUser(), id, ct));

    /// <summary>Zwolnij blokadę edycji posta</summary>
    /// <remarks>Zwalnia blokadę tylko jej posiadacz; w innym przypadku nic się nie dzieje.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="204">Blokada zwolniona (lub nie istniała).</response>
    [HttpDelete("{id:guid}/lock")]
    [Authorize(Roles = Roles.CompanyOrManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseLock(Guid id, CancellationToken ct)
    {
        await service.ReleaseLockAsync(User.ToCurrentUser(), id, ct);
        return NoContent();
    }

    /// <summary>Zaakceptuj post (klient)</summary>
    /// <remarks>Dla klienta, który sam akceptuje posty (PostApprovalMode.Client). Post w statusie AwaitingApproval jest publikowany od razu albo, gdy ma termin w przyszłości, zaplanowany. Firma i autorzy dostają powiadomienie.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <response code="200">Post po akceptacji.</response>
    /// <response code="404">Post nie istnieje albo nie jest postem konta zalogowanego klienta.</response>
    /// <response code="409">Post nie oczekuje na akceptację.</response>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<PostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PostDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await service.ApproveAsync(User.ToCurrentUser(), id, ct));

    /// <summary>Odrzuć post (klient)</summary>
    /// <remarks>Post w statusie AwaitingApproval dostaje status Rejected (zwrot do poprawy). Opcjonalne uwagi klienta (body: <c>{ "comment": "..." }</c>, do 1000 znaków) są zapisane przy poście (pole rejectionComment). Firma i autorzy dostają powiadomienie na żywo z uwagami (kliknięcie prowadzi do posta), mogą poprawić post oraz wysłać go ponownie. Body jest opcjonalne.</remarks>
    /// <param name="id">Identyfikator posta (GUID).</param>
    /// <param name="dto">Uwagi z poprawkami (opcjonalne).</param>
    /// <response code="200">Post po odrzuceniu.</response>
    /// <response code="400">Uwagi dłuższe niż 1000 znaków.</response>
    /// <response code="404">Post nie istnieje albo nie jest postem konta zalogowanego klienta.</response>
    /// <response code="409">Post nie oczekuje na akceptację.</response>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<PostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PostDto>> Reject(Guid id, [FromBody] RejectPostDto? dto, CancellationToken ct) =>
        Ok(await service.RejectAsync(User.ToCurrentUser(), id, ct, dto?.Comment));
}
