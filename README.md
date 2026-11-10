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
| `CompanyUser` | administrator firmy (rejestruje się pierwszy) | tworzy konta społecznościowe i managerów (e-mail i hasło wymagane, można je zmieniać), przypisuje managerów do kont, zarządza klientami, przegląda i tworzy posty na KAŻDYM koncie firmy (także edycja i usuwanie), dostaje powiadomienia o błędach publikacji |
| `ManagerUser` | tworzony przez firmę (zawsze z loginem) | widzi tylko przypisane konta; tworzy i edytuje posty tylko na przypisanym koncie; po utracie praw do konta może tylko przeglądać swoje dawne posty (serwer blokuje edycję i usuwanie) |
| `ClientUser`  | rejestruje się sam (osobny przycisk i ekran) | widzi firmy, z którymi jest powiązany |

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
| `POST/PUT/DELETE /api/Posts` | `CompanyUser` (każde konto firmy) i `ManagerUser` przypisany do konta (`targetAccountId` wymagane) |
| `POST/DELETE /api/Posts/{id}/lock` | blokada edycji posta (zajęcie/odnowienie, zwolnienie) |
| `POST /api/Media`, `GET /api/Media/{storageKey}` | `CompanyUser`, `ManagerUser` (upload multipart; pliki prywatne dla firmy) |
| `GET /api/Notifications`, `PUT /api/Notifications/{id}/read`, `PUT /api/Notifications/read-all` | `CompanyUser`, `ManagerUser` |
| `GET/POST/DELETE /api/ClientUsers` | `CompanyUser` (klienci firmy; powiązanie po e-mailu klienta) |
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

- **CompanyUser**: Konta, Posty, Managerowie, Klienci
- **ManagerUser**: Konta (tylko przypisane), Moje posty
- **ClientUser**: Firmy

Górne menu (wszystkie role): dzwonek powiadomień oraz menu konta (dane, rola, Profil, Wyloguj).

Formularz posta: wybór pliku (`file_picker`, upload na serwer), podgląd na żywo w stylu platformy konta,
"Publikuj teraz" / "Zaplanuj" (dzień i godzina, tylko przyszłość), blokada edycji. Lista postów ma status
(opublikowany / zaplanowany / błąd).

Widoki: tworzenie/edycja konta społecznościowego, szczegóły konta (`SocialAccountDetailScreen` - tu i tylko tu
manager przypisany do konta tworzy posty), przypisywanie managerów (`AssignManagersScreen`), tworzenie managera
(e-mail i hasło wymagane; przy edycji można zmienić e-mail i hasło), posty managera (`ManagerPostsScreen`, dostępne z listy managerów), klienci firmy
i powiązanie klienta po e-mailu (`ClientListScreen`, `ClientLinkScreen`), firmy klienta (`CompanyListScreen`).
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
  Pola: `Status` (Draft/Scheduled/Publishing/Published/Failed), `ScheduledAt`, `PublishedAt`,
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


