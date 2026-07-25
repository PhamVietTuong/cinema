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
    private readonly ISmsNotificationService _sms;
    private readonly IGoogleTokenValidator _googleValidator;
    private readonly IFacebookTokenValidator _facebookValidator;

    public AuthManager(
        IApplicationUnitOfWork uow,
        ITokenService tokenService,
        INotificationService notifications,
        ISmsNotificationService sms,
        IGoogleTokenValidator googleValidator,
        IFacebookTokenValidator facebookValidator)
    {
        _uow = uow;
        _tokenService = tokenService;
        _notifications = notifications;
        _sms = sms;
        _googleValidator = googleValidator;
        _facebookValidator = facebookValidator;
    }

    // Account-lockout policy: lock after this many consecutive failures, for this long.
    private const int _maxFailedLogins = 5;
    private static readonly TimeSpan _lockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.EmailOrPhone)
                   ?? await _uow.UserStore.GetByPhoneAsync(request.EmailOrPhone)
                   ?? throw new UnauthorizedAccessException("Invalid credentials.");

        // Reject while a lockout window is still active.
        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTime.UtcNow)
        {
            var minutes = (int)Math.Ceiling((lockoutEnd - DateTime.UtcNow).TotalMinutes);
            throw new UnauthorizedAccessException(
                $"Account locked due to too many failed attempts. Try again in {minutes} minute(s).");
        }

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            await RegisterFailedLoginAsync(user);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // Email must be confirmed before a password login is allowed.
        if (!user.EmailConfirmed)
            throw new UnauthorizedAccessException("Please verify your email address before signing in.");

        // A successful login clears any accumulated failures / lockout.
        if (user.FailedLoginCount != 0 || user.LockoutEndUtc != null)
        {
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            await _uow.UserStore.UpdateAsync(user);
            await _uow.SaveChangesAsync();
        }

        // Transparently migrate legacy (single-round HMAC) hashes to PBKDF2 on successful login.
        if (IsLegacyHash(user.PasswordSalt))
        {
            CreatePasswordHash(request.Password, out var hash, out var salt);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            await _uow.UserStore.UpdateAsync(user);
            await _uow.SaveChangesAsync();
        }

        // Two-factor: password is valid, but issue an emailed code and defer the token.
        if (user.TwoFactorEnabled)
        {
            await IssueTwoFactorCodeAsync(user);
            return new AuthResponse { RequiresTwoFactor = true };
        }

        return BuildAuthResponse(user);
    }

    // Increment the failure counter; once it hits the threshold, start a lockout window.
    private async Task RegisterFailedLoginAsync(User user)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= _maxFailedLogins)
        {
            user.LockoutEndUtc = DateTime.UtcNow.Add(_lockoutDuration);
            user.FailedLoginCount = 0; // the lockout window now governs access
        }
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
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

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = hash,
            PasswordSalt = salt,
            UserTypeId = customerType.Id,
            EmailConfirmed = false,
            EmailVerificationTokenHash = HashToken(rawToken),
            EmailVerificationExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        await _uow.UserStore.CreateAsync(user);
        await SendVerificationEmailAsync(user, rawToken);

        user = await _uow.UserStore.GetByEmailAsync(request.Email) ?? user;
        return BuildAuthResponse(user);
    }

    private Task SendVerificationEmailAsync(User user, string rawToken) =>
        _notifications.SendAsync(
            user.Email,
            "Verify your Cinema email",
            "Confirm your email address (valid 24 hours): " +
            $"/auth/verify-email?email={Uri.EscapeDataString(user.Email)}&token={rawToken}");

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.Email);
        if (user == null
            || string.IsNullOrEmpty(user.EmailVerificationTokenHash)
            || user.EmailVerificationExpiresAt == null
            || user.EmailVerificationExpiresAt < DateTime.UtcNow
            || !CryptographicOperations.FixedTimeEquals(
                   Convert.FromHexString(user.EmailVerificationTokenHash),
                   Convert.FromHexString(HashToken(request.Token))))
            throw new InvalidOperationException("Invalid or expired verification token.");

        user.EmailConfirmed = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationExpiresAt = null;
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.Email);
        // Silent if unknown (no enumeration) or already confirmed (nothing to do).
        if (user == null || user.EmailConfirmed) return;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.EmailVerificationTokenHash = HashToken(rawToken);
        user.EmailVerificationExpiresAt = DateTime.UtcNow.AddDays(1);
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        await SendVerificationEmailAsync(user, rawToken);
    }

    // Generate a 6-digit code, store its hash (5-min expiry) and email it to the user.
    private async Task IssueTwoFactorCodeAsync(User user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.TwoFactorCodeHash = HashToken(code);
        user.TwoFactorCodeExpiresAt = DateTime.UtcNow.AddMinutes(5);
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        await _notifications.SendAsync(
            user.Email,
            "Your Cinema login code",
            $"Your verification code is {code} (valid 5 minutes).");

        // Also deliver the login code by SMS when the user has a phone number (dev-log unless Twilio is configured).
        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            await _sms.SendSmsAsync(user.Phone, $"Cinema login code: {code} (valid 5 minutes).");
        }
    }

    public async Task<AuthResponse> VerifyTwoFactorAsync(VerifyTwoFactorRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.EmailOrPhone)
                   ?? await _uow.UserStore.GetByPhoneAsync(request.EmailOrPhone)
                   ?? throw new UnauthorizedAccessException("Invalid or expired code.");

        if (!user.TwoFactorEnabled
            || string.IsNullOrEmpty(user.TwoFactorCodeHash)
            || user.TwoFactorCodeExpiresAt == null
            || user.TwoFactorCodeExpiresAt < DateTime.UtcNow
            || !CryptographicOperations.FixedTimeEquals(
                   Convert.FromHexString(user.TwoFactorCodeHash),
                   Convert.FromHexString(HashToken(request.Code))))
            throw new UnauthorizedAccessException("Invalid or expired code.");

        // Consume the code so it can't be replayed.
        user.TwoFactorCodeHash = null;
        user.TwoFactorCodeExpiresAt = null;
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        return BuildAuthResponse(user);
    }

    public async Task SetTwoFactorAsync(Guid userId, bool enabled)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException("User not found.");
        user.TwoFactorEnabled = enabled;
        if (!enabled)
        {
            user.TwoFactorCodeHash = null;
            user.TwoFactorCodeExpiresAt = null;
        }
        await _uow.UserStore.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request)
    {
        var info = await _googleValidator.ValidateAsync(request.IdToken)
                   ?? throw new UnauthorizedAccessException("Invalid Google token.");

        if (!info.EmailVerified)
            throw new UnauthorizedAccessException("Google account email is not verified.");

        var user = await _uow.UserStore.GetByEmailAsync(info.Email);
        if (user == null)
        {
            var customerType = await _uow.UserTypeStore.FindSingleAsync(t => t.Name == "Customer")
                ?? throw new InvalidOperationException("Customer user type not found.");

            // The account authenticates via Google, so give it a random unusable password.
            CreatePasswordHash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), out var hash, out var salt);
            user = new User
            {
                Name = info.Name,
                Email = info.Email,
                // Phone is unique + required; Google gives us none, so store a unique placeholder.
                Phone = "g" + Guid.NewGuid().ToString("N")[..15],
                PasswordHash = hash,
                PasswordSalt = salt,
                UserTypeId = customerType.Id,
                EmailConfirmed = true, // Google already verified the address
            };
            await _uow.UserStore.CreateAsync(user);
            user = await _uow.UserStore.GetByEmailAsync(info.Email) ?? user;
        }
        else if (!user.EmailConfirmed)
        {
            // Signing in with Google proves ownership — confirm the existing account.
            user.EmailConfirmed = true;
            await _uow.UserStore.UpdateAsync(user);
            await _uow.SaveChangesAsync();
        }

        // Google logins bypass the app's own 2FA (Google enforces its own).
        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginWithFacebookAsync(FacebookLoginRequest request)
    {
        var info = await _facebookValidator.ValidateAsync(request.AccessToken)
                   ?? throw new UnauthorizedAccessException("Invalid Facebook token.");

        var user = await _uow.UserStore.GetByEmailAsync(info.Email);
        if (user == null)
        {
            var customerType = await _uow.UserTypeStore.FindSingleAsync(t => t.Name == "Customer")
                ?? throw new InvalidOperationException("Customer user type not found.");

            // The account authenticates via Facebook, so give it a random unusable password.
            CreatePasswordHash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), out var hash, out var salt);
            user = new User
            {
                Name = info.Name,
                Email = info.Email,
                // Phone is unique + required; Facebook gives us none, so store a unique placeholder.
                Phone = "f" + Guid.NewGuid().ToString("N")[..15],
                PasswordHash = hash,
                PasswordSalt = salt,
                UserTypeId = customerType.Id,
                EmailConfirmed = true, // Facebook already verified the address
            };
            await _uow.UserStore.CreateAsync(user);
            user = await _uow.UserStore.GetByEmailAsync(info.Email) ?? user;
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _uow.UserStore.UpdateAsync(user);
            await _uow.SaveChangesAsync();
        }

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

    public async Task UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesRequest request)
    {
        var user = await _uow.UserStore.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException("User not found.");
        user.NotifyBookingEmails   = request.NotifyBookingEmails;
        user.NotifyPromotionEmails = request.NotifyPromotionEmails;
        user.NotifyReminderEmails  = request.NotifyReminderEmails;
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
            TheaterId    = request.TheaterId,
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
        user.TheaterId = request.TheaterId;
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
