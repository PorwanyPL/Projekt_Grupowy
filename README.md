# Social Content (prototyp)

Aplikacja dla content managerów do zarządzania postami na social media.
Model danych: diagram klas `Projekt_grupowy_2.drawio`.

- `backend/SocialContent.Api` - ASP.NET Core 8 Web API, EF Core + SQLite, JWT, Swagger
- `frontend/social_content_app` - Flutter (Provider), ekrany: logowanie, rejestracja, Posty, Managerowie, Konta społecznościowe, Profil

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

### Zmiana schematu bazy (v1 -> v2 -> v3)

Projekt używa `EnsureCreated`, który nie zmienia istniejącej bazy. Przy starcie aplikacja
wykrywa bazę w starym schemacie i **przenosi ją** do `socialcontent.db.v1-<data>.bak`,
po czym tworzy nową bazę. Dane ze starej bazy nie są przenoszone do nowej (zostają w pliku
`.bak`). Chcesz czysty start - po prostu usuń `socialcontent.db`.

v2 -> v3 (blokada dostępu) nie traci danych: przy starcie do tabeli `ManagerCredentials` dodawana
jest kolumna `IsAccessBlocked` (domyślnie 0).

### Uwierzytelnianie (JWT)

- `POST /api/Auth/register` - rejestracja managera (imię, nazwisko, wiek, opis, e-mail, hasło)
- `POST /api/Auth/login` - logowanie; zwraca token JWT i profil
- `GET /api/Auth/me` - profil zalogowanego

Hasło: 8-128 znaków, mała i wielka litera oraz cyfra. Przechowywany jest tylko hasz PBKDF2.
Kontrolery `ManagerUsers`, `Posts` i `SocialAccounts` mają `[Authorize]` - każde żądanie wymaga
nagłówka `Authorization: Bearer <token>`. Token jest dodatkowo sprawdzany w bazie przy każdym
żądaniu: konto musi istnieć i nie może być zablokowane.

### Usuwanie, blokowanie i anonimizacja managera

- `DELETE /api/ManagerUsers/{id}` - tylko gdy manager nie ma postów ani konta społecznościowego.
  W przeciwnym razie `409 Conflict` z komunikatem "zablokuj dostęp zamiast usuwać".
- `PUT /api/ManagerUsers/{id}/access` (`{"blocked": true|false}`) - blokada/odblokowanie logowania.
  Zablokowany manager zostaje w systemie, a jego token przestaje działać od razu. Dotyczy tylko
  managerów z kontem logowania; nie można zablokować samego siebie.
- `POST /api/ManagerUsers/{id}/anonymize` - nieodwracalne: imię i nazwisko -> "Usunięty użytkownik",
  opis zastąpiony informacją o usunięciu danych, konto logowania usunięte. Powiązania z postami
  i kontem społecznościowym oraz wiek zostają.

W systemie nie ma ról - każdy zalogowany manager może blokować i anonimizować innych.

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

Token jest zapisywany w `shared_preferences` (nowa zależność) i dołączany do każdego żądania.
Odpowiedź 401 wylogowuje i przenosi na ekran logowania z komunikatem. Lista managerów ma menu
akcji: edycja, blokada/odblokowanie, anonimizacja (z potwierdzeniem) i usuwanie - przy 409 pojawia
się komunikat z API i propozycja zablokowania dostępu.

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
- `Post` n - m `ManagerUser`. `SocialAccount` 1 - n `ManagerUser`. `SocialAccount` 1 - n `Post`.
- `ManagerCredential` (poza diagramem): e-mail i hasz hasła, relacja 1:1 z `ManagerUser`.
- Prototyp: zapisywane są tylko metadane plików, tokeny kont społecznościowych są w bazie
  jawnie (na produkcji zaszyfruj je), brak integracji z API Facebooka, Instagrama i TikToka.

## Testy

```bash
cd backend && dotnet test SocialContent.Api.Tests      # xUnit, SQLite w pamięci
cd frontend/social_content_app && flutter test
```
