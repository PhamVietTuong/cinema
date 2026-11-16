using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace Cinema.Service.WebApiHost.IntegrationTests;

/// <summary>
/// Hosts the real Web API in-process against a throw-away SQL Server database. The database is created from the
/// committed <c>sql/create_db.sql</c> (so the tests also prove that script produces a schema EF can query) and is
/// dropped on dispose. The dev database is never touched.
/// </summary>
public sealed class CinemaApiFactory : WebApplicationFactory<Program>
{
    private int _dropped;
    private readonly string _serverConnection;
    private readonly string _databaseName = "CinemaIT_" + Guid.NewGuid().ToString("N")[..12];

    public CinemaApiFactory()
    {
        _serverConnection = Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariable)
            ?? throw new InvalidOperationException(RequiresSqlServerFactAttribute.EnvironmentVariable + " is not set.");
        CreateDatabase();
        ApplyEnvironment();
    }

    private string DatabaseConnection => new SqlConnectionStringBuilder(_serverConnection)
    {
        InitialCatalog = _databaseName,
    }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    // Program.cs reads JWT:Secret while building the host, before ConfigureAppConfiguration overrides apply, so the
    // token signer and the validator would disagree. Environment variables are visible from the first read.
    private void ApplyEnvironment()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__CinemaDatabase", DatabaseConnection);
        Environment.SetEnvironmentVariable("JWT__Secret", "integration-tests-only-" + new string('x', 48));
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "1000");
    }

    private void CreateDatabase()
    {
        using (var master = new SqlConnection(_serverConnection))
        {
            master.Open();
            Execute(master, "CREATE DATABASE [" + _databaseName + "]");
        }

        using var db = new SqlConnection(DatabaseConnection);
        db.Open();
        // Same order as the real setup: schema, then the seed (user types, catalog, showtimes).
        foreach (var file in new[] { "create_db.sql", "insert_db.sql" })
        {
            var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    Execute(db, batch);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || Interlocked.Exchange(ref _dropped, 1) == 1)
        {
            return; // Dispose can run twice (fixture + host); drop the database once.
        }

        SqlConnection.ClearAllPools();
        using var master = new SqlConnection(_serverConnection);
        master.Open();
        Execute(master, "ALTER DATABASE [" + _databaseName + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + _databaseName + "]");
    }

    private static void Execute(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }
}
