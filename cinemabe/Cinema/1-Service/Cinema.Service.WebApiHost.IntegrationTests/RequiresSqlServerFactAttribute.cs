namespace Cinema.Service.WebApiHost.IntegrationTests;

/// <summary>
/// A fact that needs a real SQL Server. Skipped (not failed) when <c>CINEMA_IT_SERVER_CONNECTION</c> is not set, so
/// a plain <c>dotnet test Cinema.sln</c> on a machine without SQL Server stays green.
/// </summary>
public sealed class RequiresSqlServerFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "CINEMA_IT_SERVER_CONNECTION";

    public RequiresSqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = "Set " + EnvironmentVariable + " to a SQL Server connection string " +
                   "(for example Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True) to run integration tests.";
        }
    }
}
