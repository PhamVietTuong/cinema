using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cinema.Data.Contexts;

/// <summary>
/// Lets `dotnet ef` build a CinemaContext without starting the Web API host. The host is not usable
/// at design time: Program.cs deliberately throws when JWT:Secret is absent, which every developer
/// machine without user-secrets would hit.
///
/// The connection string is only used to read/scaffold schema — `migrations add` never connects.
/// Override it with the CINEMA_DESIGNTIME_CONNECTION environment variable when running
/// `database update` / `migrations script` against a real server.
/// </summary>
public class CinemaContextDesignTimeFactory : IDesignTimeDbContextFactory<CinemaContext>
{
    private const string _fallbackConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=Cinema;Trusted_Connection=True;TrustServerCertificate=True";

    public CinemaContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CINEMA_DESIGNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = _fallbackConnection;
        }

        var options = new DbContextOptionsBuilder<CinemaContext>()
            .UseSqlServer(connection)
            .Options;

        return new CinemaContext(options);
    }
}
