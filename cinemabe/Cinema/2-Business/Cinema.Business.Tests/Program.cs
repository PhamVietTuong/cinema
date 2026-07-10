using System.Security.Cryptography;
using System.Text;
using Cinema.Business;
using Cinema.Data;
using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ─── Configuration ────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .Build();

// ─── DI ───────────────────────────────────────────────────────────────────────
var services = new ServiceCollection()
    .AddData(config)
    .AddBusiness()
    .BuildServiceProvider();

Console.WriteLine("=== Cinema Account Generator ===");
Console.WriteLine();

// ─── Admin account ────────────────────────────────────────────────────────────
await CreateAccount(services,
    name:         "Admin",
    email:        "admin@cinema.vn",
    phone:        "0900000000",
    password:     "Admin@123",
    userTypeName: "Admin",
    label:        "Admin");

// ─── Regular user account ─────────────────────────────────────────────────────
await CreateAccount(services,
    name:         "User",
    email:        "user@cinema.vn",
    phone:        "0900000001",
    password:     "User@123",
    userTypeName: "Customer",
    label:        "User");

Console.WriteLine();
Console.WriteLine("Done.");

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// ─── Create account helper ────────────────────────────────────────────────────
static async Task CreateAccount(
    IServiceProvider services,
    string name, string email, string phone, string password,
    string userTypeName, string label)
{
    try
    {
        using var scope = services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IApplicationUnitOfWork>();
        var db  = scope.ServiceProvider.GetRequiredService<CinemaContext>();

        // Skip if already exists
        if (await uow.UserStore.GetByEmailAsync(email) != null)
        {
            Console.WriteLine($"[{label}] already exists — skipped.");
            return;
        }

        // Resolve the UserType by name (Guid PK)
        var userType = await db.UserType.FirstOrDefaultAsync(ut => ut.Name == userTypeName);
        if (userType == null)
        {
            Console.WriteLine($"[{label}] UserType '{userTypeName}' not found — skipped.");
            return;
        }

        CreatePasswordHash(password, out var hash, out var salt);

        var user = new User
        {
            Name         = name,
            Email        = email,
            Phone        = phone,
            PasswordHash = hash,
            PasswordSalt = salt,
            UserTypeId   = userType.Id,
        };

        await uow.UserStore.CreateAsync(user);
        await uow.SaveChangesAsync();

        Console.WriteLine($"[{label}] created successfully!");
        Console.WriteLine($"  Email   : {email}");
        Console.WriteLine($"  Password: {password}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{label}] failed: {ex.Message}");
        var inner = ex.InnerException;
        while (inner != null) { Console.WriteLine($"  → {inner.Message}"); inner = inner.InnerException; }
    }
}

static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
{
    // Must match AuthManager's PBKDF2 parameters so seeded accounts use the strong scheme.
    salt = RandomNumberGenerator.GetBytes(16);
    hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
}
