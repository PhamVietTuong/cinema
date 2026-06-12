using System.Security.Cryptography;
using System.Text;
using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class AuthManager : IAuthManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly ITokenService _tokenService;

    public AuthManager(IApplicationUnitOfWork uow, ITokenService tokenService)
    {
        _uow = uow;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _uow.UserStore.GetByEmailAsync(request.EmailOrPhone)
                   ?? await _uow.UserStore.GetByPhoneAsync(request.EmailOrPhone)
                   ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            throw new UnauthorizedAccessException("Invalid credentials.");

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

    private static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
    {
        using var hmac = new HMACSHA512();
        salt = hmac.Key;
        hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    private static bool VerifyPassword(string password, byte[] hash, byte[] salt)
    {
        using var hmac = new HMACSHA512(salt);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return computed.SequenceEqual(hash);
    }
}
