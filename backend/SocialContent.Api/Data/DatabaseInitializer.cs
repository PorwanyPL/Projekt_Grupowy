using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SocialContent.Api.Data;

/// <summary>
/// Aplikacja używa EnsureCreated (bez migracji EF), który nie zmienia istniejącej bazy.
/// Wersja 5 dodaje prawdziwe przesyłanie plików (StorageKey, SizeKb w FileTypes), blokady edycji postów i powiadomienia.
/// Wersja 4 schematu wprowadza role (CompanyUser, ClientUser), przynależność managerów i kont do firm
/// oraz relację n - m managerów z kontami. Nie da się jej sensownie wypełnić danymi z wersji 1-3
/// (brak firmy-właściciela), dlatego stara baza jest przenoszona do pliku kopii zapasowej, a nowa powstaje od zera.
/// Wersja schematu jest zapisana w PRAGMA user_version.
/// </summary>
public static class DatabaseInitializer
{
    public const int SchemaVersion = 5;

    public static void Initialize(AppDbContext db, ILogger logger)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Brak connection stringa 'Default'.");
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (!string.IsNullOrEmpty(dataSource) && dataSource != ":memory:" && File.Exists(dataSource)
            && IsLegacySchema(connectionString))
        {
            SqliteConnection.ClearAllPools(); // zwolnij uchwyty pliku przed przeniesieniem
            var backup = $"{dataSource}.old-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(dataSource, backup);
            logger.LogWarning(
                "Wykryto bazę w starym schemacie (starszym niż v{Version}). Przeniesiono ją do '{Backup}', tworzę nową bazę '{Database}'.",
                SchemaVersion, backup, dataSource);
        }

        db.Database.EnsureCreated();
        SetSchemaVersion(connectionString);
    }

    private static bool IsLegacySchema(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var version = Convert.ToInt32(Scalar(connection, "PRAGMA user_version;"));
        var hasPosts = Convert.ToInt32(Scalar(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Posts';")) > 0;
        return version < SchemaVersion && hasPosts;
    }

    private static void SetSchemaVersion(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {SchemaVersion};";
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
