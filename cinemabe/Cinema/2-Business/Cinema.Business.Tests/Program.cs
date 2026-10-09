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

// ─── Theater staff accounts (need a theater) ─────────────────────────────────
var seedTheaterId = await GetFirstTheaterId(services);
if (seedTheaterId == null)
{
    Console.WriteLine("[Staff] No theaters found — skipped the theater staff accounts (staff, manager, boxoffice, gate, kitchen). Seed theaters, then re-run.");
}
else
{
    await CreateAccount(services,
        name:         "Theater Staff",
        email:        "staff@cinema.vn",
        phone:        "0900000002",
        password:     "Staff@123",
        userTypeName: "TheaterStaff",
        label:        "Staff",
        theaterId:    seedTheaterId);

    await CreateAccount(services,
        name:         "Theater Manager",
        email:        "manager@cinema.vn",
        phone:        "0900000003",
        password:     "Manager@123",
        userTypeName: "TheaterManager",
        label:        "Manager",
        theaterId:    seedTheaterId);

    await CreateAccount(services,
        name:         "Box Office Staff",
        email:        "boxoffice@cinema.vn",
        phone:        "0900000004",
        password:     "Box@12345",
        userTypeName: "BoxOfficeStaff",
        label:        "BoxOffice",
        theaterId:    seedTheaterId);

    await CreateAccount(services,
        name:         "Gate Staff",
        email:        "gate@cinema.vn",
        phone:        "0900000005",
        password:     "Gate@12345",
        userTypeName: "GateStaff",
        label:        "Gate",
        theaterId:    seedTheaterId);

    await CreateAccount(services,
        name:         "Kitchen Staff",
        email:        "kitchen@cinema.vn",
        phone:        "0900000006",
        password:     "Kitchen@123",
        userTypeName: "KitchenStaff",
        label:        "Kitchen",
        theaterId:    seedTheaterId);
}

// ─── Regional manager (no theater; theater assignments arrive in a later phase) ──
await CreateAccount(services,
    name:         "Regional Manager",
    email:        "regional@cinema.vn",
    phone:        "0900000007",
    password:     "Regional@123",
    userTypeName: "RegionalManager",
    label:        "Regional");

Console.WriteLine();
Console.WriteLine("Done.");

if (!Console.IsInputRedirected)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}

// ─── Create account helper ────────────────────────────────────────────────────
static async Task CreateAccount(
    IServiceProvider services,
    string name, string email, string phone, string password,
    string userTypeName, string label, Guid? theaterId = null)
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
            Name           = name,
            Email          = email,
            Phone          = phone,
            PasswordHash   = hash,
            PasswordSalt   = salt,
            UserTypeId     = userType.Id,
            TheaterId      = theaterId,
            EmailConfirmed = true, // seeded accounts are pre-verified
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

static async Task<Guid?> GetFirstTheaterId(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaContext>();
    return await db.Theater
        .AsNoTracking()
        .OrderBy(t => t.Name)
        .ThenBy(t => t.Id)
        .Select(t => (Guid?)t.Id)
        .FirstOrDefaultAsync();
}

static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
{
    // Must match AuthManager's PBKDF2 parameters so seeded accounts use the strong scheme.
    salt = RandomNumberGenerator.GetBytes(16);
    hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100_000, HashAlgorithmName.SHA256, 32);
}
