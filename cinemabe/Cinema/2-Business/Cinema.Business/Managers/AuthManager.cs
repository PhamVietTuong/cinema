using System.Security.Cryptography;
using System.Text;
using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class AuthManager : IAuthManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly ITokenService _tokenService;
    private readonly INotificationService _notifications;

    public AuthManager(IApplicationUnitOfWork uow, ITokenService tokenService, INotificationService notifications)
    {
        _uow = uow;
        _tokenService = tokenService;
        _notifications = notifications;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.EmailOrPhone)
                   ?? await _uow.UserStore.GetByPhoneAsync(request.EmailOrPhone)
                   ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            throw new UnauthorizedAccessException("Invalid credentials.");

        // Transparently migrate legacy (single-round HMAC) hashes to PBKDF2 on successful login.
        if (IsLegacyHash(user.PasswordSalt))
        {
            CreatePasswordHash(request.Password, out var hash, out var salt);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _uow.UserStore.UpdateAsync(user);
            await _uow.SaveChangesAsync();
        }

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _uow.UserStore.GetByEmailAsync(request.Email) != null)
            throw new InvalidOperationException("Email already in use.");

        if (await _uow.UserStore.GetByPhoneAsync(request.Phone) != null)
            throw new InvalidOperationException("Phone already in use.");

        CreatePasswordHash(request.Password, out var hash, out var salt);

        var customerType = await _uow.UserTypeStore.FindSingleAsync(t => t.Name == "Customer")
            ?? throw new InvalidOperationException("Customer user type not found.");

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = hash,
            PasswordSalt = salt,
            UserTypeId = customerType.Id,
        };

        await _uow.UserStore.CreateAsync(user);

        user = await _uow.UserStore.GetByEmailAsync(request.Email) ?? user;
        return BuildAuthResponse(user);
    }

    public async Task<UserDTO> GetProfileAsync(Guid userId)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException("User not found.");
        return ToUserDTO(user);
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException("User not found.");
        user.PatchEntity<User, UpdateProfileRequest>(request);
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException("User not found.");

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        CreatePasswordHash(request.NewPassword, out var hash, out var salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.Email);
        // Do not reveal whether the email exists — return silently if there's no such account.
        if (user == null) return;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetTokenHash = HashToken(rawToken);
        user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1);
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        await _notifications.SendAsync(
            user.Email,
            "Reset your Cinema password",
            $"Use this link to reset your password (valid 1 hour): " +
            $"/auth/reset-password?email={Uri.EscapeDataString(user.Email)}&token={rawToken}");
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.Email);
        if (user == null
            || string.IsNullOrEmpty(user.PasswordResetTokenHash)
            || user.PasswordResetExpiresAt == null
            || user.PasswordResetExpiresAt < DateTime.UtcNow
            || !CryptographicOperations.FixedTimeEquals(
                   Convert.FromHexString(user.PasswordResetTokenHash),
                   Convert.FromHexString(HashToken(request.Token))))
            throw new InvalidOperationException("Invalid or expired reset token.");

        CreatePasswordHash(request.NewPassword, out var hash, out var salt);
        user.PasswordHash            = hash;
        user.PasswordSalt            = salt;
        user.PasswordResetTokenHash  = null;
        user.PasswordResetExpiresAt  = null;
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<DefaultSearchResults<UserDTO>> GetUsersAsync(PagingSearchDTO search)
    {
        var searchText = search.Filters.GetString("search");
        var page       = search.PageIndex > 0 ? search.PageIndex : 1;
        var pageSize   = search.PageSize  > 0 ? search.PageSize  : 20;

        var (items, total) = await _uow.UserStore.GetPagedAsync(searchText, page, pageSize);
        return new DefaultSearchResults<UserDTO>
        {
            Results      = items.Select(ToUserDTO),
            TotalCount   = total,
            CountPerPage = pageSize,
            Page         = page
        };
    }

    public async Task<UserDTO> CreateUserAsync(CreateUserRequest request)
    {
        if (await _uow.UserStore.GetByEmailAsync(request.Email) != null)
            throw new InvalidOperationException("Email already in use.");
        if (await _uow.UserStore.GetByPhoneAsync(request.Phone) != null)
            throw new InvalidOperationException("Phone already in use.");

        var userTypeId = request.UserTypeId != Guid.Empty
            ? request.UserTypeId
            : (await _uow.UserTypeStore.FindSingleAsync(t => t.Name == "Customer")
               ?? throw new InvalidOperationException("Customer user type not found.")).Id;

        CreatePasswordHash(request.Password, out var hash, out var salt);
        var user = new User
        {
            Name         = request.Name,
            Email        = request.Email,
            Phone        = request.Phone,
            PasswordHash = hash,
            PasswordSalt = salt,
            UserTypeId   = userTypeId,
            Status       = request.Status,
        };
        await _uow.UserStore.CreateAsync(user);
        return ToUserDTO(await _uow.UserStore.GetByIdAsync(user.Id) ?? user);
    }

    public async Task<UserDTO> UpdateUserAsync(UpdateUserRequest request)
    {
        var user = await _uow.UserStore.GetByIdAsync(request.Id)
                   ?? throw new KeyNotFoundException("User not found.");
        user.Name   = request.Name;
        user.Phone  = request.Phone;
        user.Status = request.Status;
        if (request.Avatar != null) user.Avatar = request.Avatar;
        if (request.UserTypeId != Guid.Empty) user.UserTypeId = request.UserTypeId;
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
        return ToUserDTO(await _uow.UserStore.GetByIdAsync(request.Id) ?? user);
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _uow.UserStore.GetByIdAsync(id)
                   ?? throw new KeyNotFoundException("User not found.");
        // Soft-delete: deactivating preserves the user's invoices/comments history.
        user.Status = UserStatus.Inactive;
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    private AuthResponse BuildAuthResponse(User user) => new()
    {
        Token = _tokenService.GenerateToken(user),
        ExpiresAt = _tokenService.GetTokenExpiry(),
        User = ToUserDTO(user)
    };

    private static UserDTO ToUserDTO(User user)
    {
        var dto = user.ToDTO<User, UserDTO>();
        dto.UserTypeName = user.UserType?.Name ?? string.Empty;
        dto.MemberShipName = user.MemberShip?.Name;
        return dto;
    }

    // PBKDF2 (SHA-256) key-stretching parameters.
    private const int    _pbkdf2SaltSize   = 16;
    private const int    _pbkdf2KeySize    = 32;
    private const int    _pbkdf2Iterations = 100_000;
    private static readonly HashAlgorithmName _pbkdf2Algorithm = HashAlgorithmName.SHA256;

    private static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
    {
        salt = RandomNumberGenerator.GetBytes(_pbkdf2SaltSize);
        hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, _pbkdf2Iterations, _pbkdf2Algorithm, _pbkdf2KeySize);
    }

    private static bool VerifyPassword(string password, byte[] hash, byte[] salt)
    {
        if (IsLegacyHash(salt))
        {
            // Legacy scheme: single-round HMAC-SHA512 keyed by the stored salt.
            using var hmac = new HMACSHA512(salt);
            var legacy = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(legacy, hash);
        }

        var computed = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, _pbkdf2Iterations, _pbkdf2Algorithm, _pbkdf2KeySize);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    // New PBKDF2 salts are exactly _pbkdf2SaltSize bytes; the old HMAC-SHA512 key salts are 128 bytes.
    private static bool IsLegacyHash(byte[] salt) => salt.Length != _pbkdf2SaltSize;
}
