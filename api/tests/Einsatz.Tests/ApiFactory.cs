using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Einsatz.Tests;

/// <summary>
/// Boots the real API against a throw-away PostgreSQL database (migrated and seeded on start).
/// The server comes from EINSATZ_TEST_DB, e.g. "Host=db;Username=einsatz;Password=einsatz".
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _database = $"einsatz_test_{Guid.NewGuid():N}";
    public string DataDir { get; } = Directory.CreateTempSubdirectory("einsatz-").FullName;

    public static string DbServer =>
        Environment.GetEnvironmentVariable("EINSATZ_TEST_DB") ?? "Host=localhost;Username=einsatz;Password=einsatz";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (not ConfigureAppConfiguration) so values are visible while Program.cs is still building.
        builder.UseSetting("ConnectionStrings:Db", $"{DbServer};Database={_database}");
        builder.UseSetting("Integrations:ErpImportPath", Path.Combine(DataDir, "erp"));
        builder.UseSetting("Integrations:ArchivePath", Path.Combine(DataDir, "dms"));
        builder.UseSetting("DataProtection:Path", Path.Combine(DataDir, "keys"));
        builder.UseSetting("Demo:NightlyReset", "false");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await using var conn = new Npgsql.NpgsqlConnection($"{DbServer};Database=postgres");
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
        Directory.Delete(DataDir, recursive: true);
    }
}
