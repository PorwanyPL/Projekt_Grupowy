using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SocialContent.Api.Data;

/// <summary>
/// Aplikacja używa EnsureCreated (bez migracji EF), który nie zmienia istniejącej bazy.
/// Wersja 1 schematu (przed modelem z SocialAccount i podklasami Post) nie jest zgodna z wersją 2,
/// dlatego stara baza jest przenoszona do pliku kopii zapasowej, a nowa powstaje od zera.
/// Wersja 3 dodaje tylko kolumnę ManagerCredentials.IsAccessBlocked - baza v2 jest uzupełniana w miejscu (ALTER TABLE).
/// Wersja schematu jest zapisana w PRAGMA user_version.
/// </summary>
public static class DatabaseInitializer
{
    public const int SchemaVersion = 3;

    public static void Initialize(AppDbContext db, ILogger logger)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Brak connection stringa 'Default'.");
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (!string.IsNullOrEmpty(dataSource) && dataSource != ":memory:" && File.Exists(dataSource)
            && IsLegacySchema(connectionString))
        {
            SqliteConnection.ClearAllPools(); // zwolnij uchwyty pliku przed przeniesieniem
            var backup = $"{dataSource}.v1-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(dataSource, backup);
            logger.LogWarning(
                "Wykryto bazę w starym schemacie (v1). Przeniesiono ją do '{Backup}', tworzę nową bazę '{Database}' w schemacie v{Version}.",
                backup, dataSource, SchemaVersion);
        }

        db.Database.EnsureCreated();
        UpgradeToV3(connectionString);
        SetSchemaVersion(connectionString);
    }

    /// <summary>Baza w wersji 2 nie ma kolumny blokady dostępu - dodajemy ją, istniejące konta są odblokowane.</summary>
    private static void UpgradeToV3(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var tableExists = Convert.ToInt32(Scalar(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ManagerCredentials';")) > 0;
        if (!tableExists) return;

        var columnExists = Convert.ToInt32(Scalar(connection,
            "SELECT COUNT(*) FROM pragma_table_info('ManagerCredentials') WHERE name = 'IsAccessBlocked';")) > 0;
        if (columnExists) return;

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE ManagerCredentials ADD COLUMN IsAccessBlocked INTEGER NOT NULL DEFAULT 0;";
        command.ExecuteNonQuery();
    }

    private static bool IsLegacySchema(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var version = Convert.ToInt32(Scalar(connection, "PRAGMA user_version;"));
        var hasPosts = Convert.ToInt32(Scalar(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Posts';")) > 0;
        return version < 2 && hasPosts;
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
