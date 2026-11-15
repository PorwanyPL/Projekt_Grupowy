Karta projektu (.docx): https://studentpcz-my.sharepoint.com/:w:/g/personal/s139525ps_student_pcz_pl/IQDu_jqCBRnaQLGrSGGQ0GpPATaVHmSqIfFJ6hqRKWjWubI?rtime=rvVfPwMt30g

Karta projektu (.pdf): karta_projektu_d_s.pdf

# Social Content (prototyp)

Aplikacja dla content managerów do zarządzania postami na social media.
Model danych: diagram klas `Projekt_grupowy_2.drawio`.

- `backend/SocialContent.Api` - ASP.NET Core 8 Web API, EF Core + SQLite, JWT z rolami, AutoMapper, Swagger
- `frontend/social_content_app` - Flutter (Provider); ekrany zależą od roli (firma, manager, klient)

## Struktura backendu

```
SocialContent.Api/
  Domain/
    Entities/      jedna klasa = jeden plik (ManagerUser, CompanyUser, ClientUser, Post, FacebookPost, ...)
    ValueObjects/  FirstName, LastName, Age, Description, Title, ...
    Enums/         Format, FacebookPostType, PostStatus, SocialPlatform, UserRole, ...
    Exceptions/    DomainValidationException, NotFoundException, ConflictException, ForbiddenException, ...
    Validate.cs    wszystkie metody walidacyjne (jedyne miejsce z logiką walidacji), m.in. MediaUpload,
                   NormalizeTags, VerifyConnection, StorageKey
  Mapping/         jeden profil AutoMappera na klasę: [Klasa]Profile.cs
  Dtos/            jeden plik na DTO
  Services/        logika aplikacji (serwisy używają IMapper)
  Controllers/ Data/ Middleware/ Swagger/
```

Przestrzeń nazw odpowiada folderowi (np. `SocialContent.Api.Domain.Entities`).

## Role

| Rola          | Kto                                  | Co może |
|---------------|--------------------------------------|---------|
| `CompanyUser` | administrator firmy (rejestruje się pierwszy) | tworzy managerów, przyjmuje/odrzuca zaproszenia do obsługi profili klientów, nadaje klientom aliasy, przypisuje managerów do profili, przegląda i tworzy posty na profilach, które przyjęła (także edycja opublikowanych i usuwanie), dostaje powiadomienia o błędach publikacji |
| `ManagerUser` | tworzony przez firmę (zawsze z loginem) | widzi tylko klientów i profile, do których jest przypisany; tworzy i edytuje posty tylko na przypisanym profilu; po utracie praw może tylko przeglądać swoje dawne posty |
| `ClientUser`  | rejestruje się sam | tworzy własne profile społecznościowe i zaprasza firmę (e-mail), widzi wszystkie posty swoich profili tylko do odczytu, wybiera dla każdej firmy tryb zatwierdzania postów i (w trybie "klient") zatwierdza/odrzuca posty |

Relacje (diagram + opis słowny): `CompanyUser` 1-n `ManagerUser`, `CompanyUser` 1-n `SocialAccount`,
`ManagerUser` n-m `SocialAccount` (przypisanie uprawnia do tworzenia postów), `ManagerUser` n-m `Post`
(autorzy), `CompanyUser` n-m `ClientUser`. Rola jest w tokenie JWT (claim `role`), a dostęp pilnują
atrybuty `[Authorize(Roles = ...)]` oraz zakres danych w serwisach (izolacja po `CompanyId`).

## 1. Backend

Wymagania: .NET SDK 8 lub nowszy.

```bash
cd backend/SocialContent.Api
dotnet restore
dotnet run
```

- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger (przycisk **Authorize** przyjmuje token JWT)
- Baza `socialcontent.db` (SQLite) tworzy się sama.

### Wersja 7 - kalendarz, post na wiele platform, tagi, filtry

- **Usunięto dodawanie klienta przez firmę**: nie ma już `POST /api/ClientUsers` (ani `LinkClientDto`, `LinkAsync`,
  ekranu `ClientLinkScreen` i przycisku "Dodaj klienta"). Relacja firma - klient powstaje wyłącznie, gdy firma
  przyjmie zaproszenie klienta (`POST /api/SocialAccounts/{id}/accept`). Zostają: lista klientów, alias, odpięcie.
- **Post na wiele platform**: `POST/PUT /api/Posts` przyjmują `targetAccountIds` (lista kont; pojedyncze
  `targetAccountId` nadal działa). Każde konto to osobny wariant `Post` (TPH), ale warianty mają wspólny `GroupId`
  i są jednym logicznym postem: `PostDto.id` to `GroupId`, a `PostDto.targets` zawiera warianty (konto, platforma,
  status). Blokada edycji, powiadomienia, zatwierdzanie, odrzucanie i usuwanie działają na całej grupie.
  Status grupy to status zbiorczy (Publishing > Failed > AwaitingApproval > Rejected > Scheduled > Draft > Published).
- **Edycja nie cofa do akceptacji**: zapis zaplanowanego lub opublikowanego wariantu zachowuje jego status
  (tylko nowo dodane warianty przechodzą zwykły obieg akceptacji).
- **Tagi treści** (nowe pole `tags`, maks. 10 po 30 znaków, bez wielkości liter, `#` jest obcinany).
- **Filtry** `GET /api/Posts`: `status` (wiele), `tag` (wiele), `accountIds` (wiele), `platform` (wiele),
  `managerId`, `from`/`to` (dzień publikacji, a bez niego termin lub dzień utworzenia), `type`, `accountId`.
  Kategorie łączą się koniunkcją, wartości w kategorii alternatywą; widoczność ról jest zawsze nakładana
  przed filtrami. `PostDto.counterpartName` to klient (alias lub imię i nazwisko) dla firmy i managera albo
  firma dla klienta.

### Zmiana schematu bazy (v7)

`Post` ma nowe kolumny `GroupId` (indeks) i `TagsText`, więc zmienia się schemat: `SchemaVersion = 7`.
Stara baza jest przenoszona do `<baza>.old-<data>.bak`, a nowa tworzy się od zera i (w środowisku Development)
jest zasilana seederem (dochodzą: post na Facebooku i Instagramie jako jedna grupa, opublikowany post, tagi).

### Wersja 6 - klient tworzy profile, zaproszenia firm, akceptacja postów

Model (zgodny ze schematem `Projekt_grupowy_2`: `SocialAccount` ma jednego klienta i jedną firmę, `CompanyUser` n-m `ClientUser`):

- `SocialAccount` należy do klienta (`OwnerClientId`) i jest przypisany do firmy (`CompanyId`) ze stanem zaproszenia
  `ManagementStatus`: `Invited` -> `Accepted` / `Declined`. Managerów można przypisać dopiero po `Accepted`.
  (W schemacie pole nazywa się `ClientUser`; w kodzie `OwnerClient` - to ta sama relacja.)
- `CompanyClient` (jawna tabela relacji n-m firma - klient) trzyma `Alias` klienta nadany przez firmę oraz
  `ApprovalMode` (`Manager` - domyślnie, firma publikuje bez klienta; `Client` - posty czekają na akceptację klienta).
  Przyjęcie zaproszenia tworzy to powiązanie, jeśli go nie było.
- Statusy postów: doszły `AwaitingApproval` i `Rejected`. Przy trybie `Client` nowy/edytowany post firmy trafia do
  klienta (`POST /api/Posts/{id}/approve|reject`, tylko właściciel konta); zatwierdzenie publikuje (lub planuje) post,
  odrzucenie ustawia `Rejected`. `PostDto.canApprove` mówi, czy zalogowany klient może zdecydować.
- Opublikowane posty można edytować (zapis nie publikuje ponownie); blokuje tylko status `Publishing`.
- Endpointy: `POST /api/SocialAccounts` (klient, pole `companyEmail`), `POST /api/SocialAccounts/{id}/invite`
  (klient), `POST .../accept|decline` (firma), `PUT .../managers` (firma, tylko `Accepted`),
  `PUT /api/ClientUsers/{id}/alias` (firma), `GET /api/ClientUsers/me/companies` i
  `PUT /api/ClientUsers/me/companies/{companyId}/approval` (klient). `GET /api/ClientUsers` zwraca klientów firmy
  (powiązanych lub z zaproszeniem), a dla managera - tylko właścicieli przypisanych profili (bez e-maili).
- Autoryzacja po stronie serwera: klient nie edytuje postów (403), firma widzi tylko swoich klientów i profile
  (bez odrzuconych zaproszeń), manager tylko przypisane i przyjęte, klient tylko własne.
- Odtwarzanie mediów: `GET /api/Media/...` przyjmuje też `?access_token=` (tylko GET; dla odtwarzacza wideo),
  obsługuje zakresy bajtów (przewijanie).

### Zmiana schematu bazy (v6)

Projekt nie używa migracji EF (`EnsureCreated` + `PRAGMA user_version`), więc zmianę modelu obsługuje mechanizm
wersji schematu: `SchemaVersion = 6` w `DatabaseInitializer`. Przy starcie stara baza jest przenoszona do pliku
`.bak`, a nowa powstaje od zera (dane nie są przenoszone; w Development seeder dodaje dane demo).
Zmiany: tabela `CompanyClients`; w `SocialAccounts` kolumny `OwnerClientId`, `CompanyId`, `ManagementStatus`
i unikalność `(OwnerClientId, Platform, AccountId)`. Aby przejść na prawdziwe migracje, wystarczy zamienić
`EnsureCreated` na `dotnet ef migrations add` po ustaleniu docelowego schematu.

### Wersja 5 - media, publikacja, blokady, powiadomienia

- **Upload mediów**: `POST /api/Media` (multipart, pole `file`). Serwer sprawdza rozszerzenie (png, jpg, jpeg, gif,
  webp, mp4, mp3, flac), rozmiar (zdjęcia do 10 MB, wideo do 50 MB) i sygnaturę pliku. Pliki trafiają do
  `uploads/{companyId}/{klucz}`; w poście podaje się `storageKey` + `fileName`, a format i rozmiar ustala serwer.
  Pole "rozmiar w KB" znikło z formularza; długość wideo jest nieznana (`length` może być null).
- **Współautorzy**: autorami nowego posta są od razu wszyscy managerowie przypisani do konta (`managerUserIds` nie
  ma już w żądaniu). Przy edycji dotychczasowi autorzy zostają, a przypisani do konta dochodzą.
- **Blokada edycji**: wiersz `PostEditLocks` (klucz = id posta, ważność 2 min, odnawiana co minutę przez formularz,
  zwalniana przy zapisie/wyjściu i wygasająca sama). Drugi użytkownik dostaje `409` z imieniem edytującego.
- **Publikacja**: `publishNow=true` publikuje od razu, `scheduledAt` (w przyszłości) planuje; `BackgroundService`
  co 30 s publikuje zaległe zaplanowane posty. Publikacja jest SYMULOWANA (brak API platform): sprawdza lokalnie
  `Validate.VerifyConnection`; przy błędzie post dostaje status `Failed`, a firma i autorzy posta powiadomienie.
- **Uprawnienia**: `PostDto.canEdit/canDelete` liczy serwer; `PUT/DELETE` bez prawa dają `403`/`404`.

### Zmiana schematu bazy (v5, wcześniej v4)

Wersja 5 dodaje tabele `PostEditLocks` i `Notifications` oraz kolumny `SizeKb`/`StorageKey` w plikach, więc
zmienia schemat: stara baza jest przenoszona do `.bak`, dane nie są przenoszone (patrz niżej, mechanizm z v4).

### Zmiana schematu bazy (v4)

Projekt używa `EnsureCreated`, który nie zmienia istniejącej bazy. Wersja 4 wprowadza firmy, klientów
i role, więc zmienia schemat (`ManagerUser` i `SocialAccount` mają teraz `CompanyId`, relacja manager - konto
jest n-m). Przy starcie aplikacja wykrywa bazę w starszym schemacie i **przenosi ją** do
`socialcontent.db.old-<data>.bak`, po czym tworzy nową bazę. Dane nie są przenoszone. Czysty start - usuń
`socialcontent.db`.

### Uwierzytelnianie (JWT)

- `POST /api/Auth/register-company` - rejestracja firmy (nazwa, e-mail, hasło); zwraca token i profil
- `POST /api/Auth/register-client` - samodzielna rejestracja klienta (imię, nazwisko, wiek, opis, e-mail, hasło)
- `POST /api/Auth/login` - logowanie wszystkich ról; zwraca token (z rolą) i profil
- `GET /api/Auth/me` - profil zalogowanego

Managerowie nie rejestrują się sami: tworzy ich firma (`POST /api/ManagerUsers`, e-mail i hasło są wymagane;
zmiana: `PUT /api/ManagerUsers/{id}/credentials`). E-mail jest unikalny globalnie (we wszystkich rolach). Hasło: 8-128 znaków, mała i wielka
litera oraz cyfra; przechowywany jest tylko hasz PBKDF2. Token jest dodatkowo sprawdzany w bazie przy każdym
żądaniu: konto musi istnieć i nie może być zablokowane.

### Endpointy wg ról

| Endpoint | Rola |
|----------|------|
| `/api/ManagerUsers` (CRUD, `/access`, `/anonymize`) | `CompanyUser` (tylko managerowie własnej firmy) |
| `GET /api/SocialAccounts[/{id}]` | `CompanyUser` (wszystkie konta firmy), `ManagerUser` (tylko przypisane) |
| `POST/PUT/DELETE /api/SocialAccounts`, `PUT /api/SocialAccounts/{id}/managers` | `CompanyUser` |
| `GET /api/Posts?accountId=&managerId=&type=` | `CompanyUser` (posty firmy), `ManagerUser` (tylko własne) |
| `POST/PUT/DELETE /api/Posts` | `CompanyUser` (każde konto firmy) i `ManagerUser` przypisany do konta (`targetAccountIds` wymagane; kilka kont = jeden post) |
| `POST/DELETE /api/Posts/{id}/lock` | blokada edycji posta (zajęcie/odnowienie, zwolnienie) |
| `POST /api/Media`, `GET /api/Media/{storageKey}` | `CompanyUser`, `ManagerUser` (upload multipart; pliki prywatne dla firmy) |
| `GET /api/Notifications`, `PUT /api/Notifications/{id}/read`, `PUT /api/Notifications/read-all` | `CompanyUser`, `ManagerUser` |
| `GET/DELETE /api/ClientUsers`, `PUT /api/ClientUsers/{id}/alias` | `CompanyUser` (klienci firmy; relacja powstaje z zaproszenia klienta) |
| `GET /api/ClientUsers/me/companies` | `ClientUser` |

Brak uprawnień kończy się `403`, brak/niepoprawny token `401`.

### Usuwanie, blokowanie i anonimizacja managera

- `DELETE /api/ManagerUsers/{id}` - tylko gdy manager nie ma postów ani kont społecznościowych.
  W przeciwnym razie `409 Conflict` z komunikatem "zablokuj dostęp zamiast usuwać".
- `PUT /api/ManagerUsers/{id}/access` (`{"blocked": true|false}`) - blokada/odblokowanie logowania.
  Zablokowany manager zostaje w systemie, a jego token przestaje działać od razu. Dotyczy tylko
  managerów z kontem logowania.
- `POST /api/ManagerUsers/{id}/anonymize` - nieodwracalne: imię i nazwisko -> "Usunięty użytkownik",
  opis zastąpiony informacją o usunięciu danych, konto logowania usunięte. Powiązania z postami
  i kontami społecznościowymi oraz wiek zostają.

Klucz podpisu tokenów (`Jwt:Key` w `appsettings.json`) jest kluczem deweloperskim.
Na produkcji ustaw własny, min. 32 znaki, np. zmienną środowiskową `Jwt__Key`.

Inna baza (np. SQL Server): podmień pakiet na `Microsoft.EntityFrameworkCore.SqlServer`,
w `Program.cs` zamień `UseSqlite` na `UseSqlServer`, zmień connection string, usuń wywołanie
`DatabaseInitializer` (jest specyficzny dla SQLite) i przejdź na migracje EF.

## 2. Frontend

Wymagania: Flutter SDK 3.22 lub nowszy.

Paczka zawiera kod (`lib/`, `pubspec.yaml`, `test/`), ale bez folderów platform
(android/ios/web...). Wygeneruj je jednorazowo:

```bash
cd frontend/social_content_app
flutter create . --project-name social_content_app --platforms=web,android,windows
flutter pub get
flutter run
```

`flutter create .` nie nadpisuje istniejących plików `lib/main.dart` i `pubspec.yaml`.

Token jest zapisywany w `shared_preferences` i dołączany do każdego żądania. Odpowiedź 401 wylogowuje
i przenosi na ekran logowania z komunikatem.

Ekran logowania ma dwa osobne przyciski rejestracji: **Zarejestruj firmę** (`RegisterCompanyScreen`)
i **Zarejestruj się jako klient** (`RegisterClientScreen`). Dolna nawigacja zależy od roli:

- **CompanyUser**: Konta (lista klientów -> ich profile, zaproszenia, aliasy), Kalendarz, Posty (lista z filtrami), Managerowie
- **ManagerUser**: Konta (klienci z przypisanymi profilami), Kalendarz, Moje posty
- **ClientUser**: Moje profile, Kalendarz (podgląd i akceptacja), Firmy (tryb zatwierdzania postów + profile firmy)

Każda zakładka ma własny `Navigator`, więc dolne menu i górne menu konta są widoczne na każdym ekranie.
Dane odświeżają się przy każdym przejściu między widokami (`AutoRefresh`, `RouteObserver`) i co minutę
(polling). Błędy trafiają do `AppMessenger`: identyczny komunikat nie powtarza się, a różne układają się
w stos od dołu do góry. Podgląd posta ma odtwarzacz wideo/audio (`video_player`, na Windows `video_player_win`).

Górne menu (wszystkie role): dzwonek powiadomień oraz menu konta (dane, rola, Profil, Wyloguj).

Kalendarz (`CalendarScreen`) jest pierwszym ekranem tworzenia postów: siatka miesiąca z przyciskami "Dzisiaj",
"Filtry" i "Utwórz post". Kliknięcie dnia otwiera formularz z terminem publikacji ustawionym na ten dzień
(12:00, do zmiany; dni z przeszłości nie pozwalają planować). Komórka dnia pokazuje kafelki postów: tytuł
(lewy górny róg), inicjały managerów (prawy górny), klient albo firma (lewy dolny) i ikony platform (prawy dolny).
Kliknięcie kafelka otwiera ten sam formularz w trybie edycji (blokada `PostEditLock`; klient dostaje podgląd
z przyciskami zatwierdzenia). Filtry kalendarza: status, osoba przypisana, tagi, profile i platformy.

Formularz posta: wybór klienta i platform (ikony kont klienta; wiele platform = jeden post), tagi, wybór pliku
(`file_picker`, upload na serwer), podgląd na żywo w stylu wybranej platformy, "Publikuj teraz" / "Zaplanuj"
(dzień i godzina, tylko przyszłość) / "Zapisz jako szkic", blokada edycji. Lista postów (`PostsView`) ma filtry
(status, zakres dat, platforma), a post na kilka platform jest jedną pozycją z ikonami wszystkich platform.
Podgląd wideo w formularzu startuje po kliknięciu, z limitem czasu inicjalizacji i przyciskiem ponowienia.

Widoki: tworzenie/edycja konta społecznościowego, szczegóły konta (`SocialAccountDetailScreen` - tu i tylko tu
manager przypisany do konta tworzy posty), przypisywanie managerów (`AssignManagersScreen`), tworzenie managera
(e-mail i hasło wymagane; przy edycji można zmienić e-mail i hasło), posty managera (`ManagerPostsScreen`, dostępne z listy managerów), klienci firmy
(`ClientListScreen`: alias i odpięcie), firmy klienta (`CompanyListScreen`).
Lista managerów ma menu akcji: posty, edycja, blokada/odblokowanie, anonimizacja i usuwanie
(przy 409 komunikat z API i propozycja blokady).

### Adres API

| Platforma              | Adres                  |
|------------------------|------------------------|
| Web, desktop, iOS sym. | http://localhost:5080  |
| Emulator Androida      | http://10.0.2.2:5080   |

Własny adres (np. fizyczny telefon w tej samej sieci):

```bash
flutter run --dart-define=API_BASE_URL=http://192.168.1.20:5080
```

## Model danych (skrót)

- Value objecty: `FirstName`, `LastName`, `Age`, `Description`, `Title`, `CreatedAt`, `FileName`,
  `ScheduledTime`, `ExternalId`, `WebUrl` - fabryki `Create` z walidacją, zapis jako zwykłe kolumny.
- `Post` (ogólny) i jego podklasy `FacebookPost`, `InstagramPost`, `TikTokPost` - jedna tabela (TPH).
  Pola: `GroupId` (wspólny dla wariantów jednego posta), `TagsText`, `Status` (Draft/Scheduled/Publishing/Published/Failed/AwaitingApproval/Rejected), `ScheduledAt`, `PublishedAt`,
  `ExternalPostId`, `ErrorMessage`, `TargetAccount`.
- `FileType` -> `Picture`, `Video` (TPH). `Post` 1 - 0..n zdjęć i wideo.
- `Post` n - m `ManagerUser` (autorzy). `SocialAccount` n - m `ManagerUser` (uprawnienie do tworzenia postów).
  `SocialAccount` 1 - n `Post`. `CompanyUser` 1 - n `ManagerUser`, `CompanyUser` 1 - n `SocialAccount`,
  `CompanyUser` n - m `ClientUser`.
- `ManagerCredential` (poza diagramem): e-mail i hasz hasła, relacja 1:1 z `ManagerUser`.
  Dane logowania `CompanyUser` i `ClientUser` (e-mail, hasz) są w ich własnych tabelach.
- `PostEditLock`, `Notification` (v5): blokada edycji i powiadomienia o błędach publikacji.
- Prototyp: pliki leżą na dysku serwera (`uploads/`), tokeny kont społecznościowych są w bazie
  jawnie (na produkcji zaszyfruj je), brak integracji z API Facebooka, Instagrama i TikToka.

## Testy

```bash
cd backend && dotnet test SocialContent.Api.Tests      # xUnit, SQLite w pamięci
cd frontend/social_content_app && flutter test
```

## v8 – czat i powiadomienia na żywo (SignalR)

- **Schemat bazy: 7 → 8** (`DatabaseInitializer.SchemaVersion = 8`). Nowe: `Conversations`, `ConversationParticipants`, `ChatMessages`, kolumna `CompanyUsers.ChatManagerId`. Stara baza jest przenoszona do `.bak`, nowa powstaje od zera i jest zasilana seederem.
- **Hub:** `/hubs/chat` (JWT w `?access_token=`). Zdarzenia serwer → klient: `ReceiveMessage`, `ReceiveNotification`. Metoda huba: `SendMessage(conversationId, text)`.
- **REST (`api/Chat`):** `GET contacts`, `GET conversations`, `POST conversations/direct`, `POST groups`, `GET conversations/{id}/messages`, `POST conversations/{id}/messages`, `POST conversations/{id}/read`, `GET/PUT chat-manager` (tylko firma).
- **Reguły uprawnień (serwer, przy każdym odczycie/wysłaniu):** (1) firma wskazuje jednego managera ds. czatu, który pisze ze WSZYSTKIMI klientami; (2) pozostali managerowie – tylko z klientami współpracującymi z ich firmą (CompanyClient) i osobami z własnej firmy; (3) firma – ze wszystkimi poza managerami innych firm; (4) dotychczasowe ograniczenia widoczności bez zmian. Grupa = firma + managerowie jednej firmy.
- **Powiadomienia:** `NotificationPushInterceptor` wysyła każde nowo zapisane `Notification` przez SignalR; dodano powiadomienie o publikacji posta; dzwonek jest teraz dla wszystkich ról (także klienta).
- **Kestrel:** `http://[::]:5080` (IPv4 + IPv6), a klient domyślnie używa `http://127.0.0.1:5080` – usuwa błąd WinHTTP 12029 w podglądzie wideo na Windows.
- **Flutter:** pakiet `signalr_netcore`; zakładka „Czat” dla każdej roli; testy `test/v8_test.dart`; backend: `ChatTests.cs`.
- Konta demo: firma@/anna@/bartek@/klient@/klient2@/klient3@demo.pl, hasło `ToJestHaslo1!` (anna = manager ds. czatu, klient3 bez współpracy z firmą).
