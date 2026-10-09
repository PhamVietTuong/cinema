using System.Text.Json;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Security;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class ManagerOverrideService : IManagerOverrideService
{
    private const int _maxFailedAttempts = 5;
    private static readonly TimeSpan _lockoutDuration = TimeSpan.FromMinutes(15);
    private const int _minPinLength = 4;
    private const int _maxPinLength = 8;

    private readonly IApplicationUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public ManagerOverrideService(IApplicationUnitOfWork uow, IAuditLogger audit)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<Guid> VerifyAsync(Guid theaterId, Guid actorUserId, ManagerOverrideDTO? overrideDto, AuditAction action)
    {
        var actor = await _uow.UserStore.GetByIdAsync(actorUserId);
        if (actor != null && IsApproverFor(actor, theaterId))
        {
            return actor.Id;
        }

        if (overrideDto == null)
        {
            throw new AccessDeniedException("Manager approval is required for this action.");
        }

        var approver = await _uow.UserStore.GetByIdAsync(overrideDto.ApproverUserId);
        if (approver == null || approver.Status != UserStatus.Active || !IsApproverFor(approver, theaterId))
        {
            await RecordFailureAsync(theaterId, actorUserId, overrideDto.ApproverUserId, action, "ApproverNotEligible");
            throw new AccessDeniedException("Manager approval was refused.");
        }

        var now = DateTime.UtcNow;
        if (approver.OverridePinLockoutEndUtc is { } lockoutEnd)
        {
            if (lockoutEnd > now)
            {
                await RecordFailureAsync(theaterId, actorUserId, approver.Id, action, "PinLockedOut");
                throw new AccessDeniedException("Manager approval was refused.");
            }
            // The lockout window has passed: start counting afresh.
            approver.OverridePinLockoutEndUtc = null;
            approver.OverridePinFailedCount = 0;
        }

        if (approver.OverridePinHash == null || approver.OverridePinSalt == null
            || !PasswordHasher.Verify(overrideDto.Pin, approver.OverridePinHash, approver.OverridePinSalt))
        {
            approver.OverridePinFailedCount++;
            if (approver.OverridePinFailedCount >= _maxFailedAttempts)
            {
                approver.OverridePinLockoutEndUtc = now.Add(_lockoutDuration);
            }
            await _uow.UserStore.UpdateAsync(approver);
            await RecordFailureAsync(theaterId, actorUserId, approver.Id, action, "WrongPin");
            throw new AccessDeniedException("Manager approval was refused.");
        }

        if (approver.OverridePinFailedCount != 0 || approver.OverridePinLockoutEndUtc != null)
        {
            approver.OverridePinFailedCount = 0;
            approver.OverridePinLockoutEndUtc = null;
            await _uow.UserStore.UpdateAsync(approver);
            await _uow.SaveChangesAsync();
        }
        return approver.Id;
    }

    public async Task SetPinAsync(Guid userId, string pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length < _minPinLength || pin.Length > _maxPinLength || !pin.All(char.IsAsciiDigit))
        {
            throw new InvalidOperationException($"The PIN must be {_minPinLength} to {_maxPinLength} digits.");
        }

        var user = await _uow.UserStore.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        PasswordHasher.CreateHash(pin, out var hash, out var salt);
        user.OverridePinHash = hash;
        user.OverridePinSalt = salt;
        user.OverridePinFailedCount = 0;
        user.OverridePinLockoutEndUtc = null;
        await _uow.UserStore.UpdateAsync(user);

        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = user.TheaterId,
            ActorUserId = userId,
            Action = AuditAction.OverridePinChanged,
            EntityType = nameof(User),
            EntityId = userId
        });
        await _uow.SaveChangesAsync();
    }

    public async Task<List<OverrideApproverDTO>> GetApproversAsync(Guid theaterId)
    {
        var approvers = await _uow.UserStore.GetApproversAsync(
            theaterId,
            Array.Empty<string>(),
            new[] { RoleNames.Admin });
        return approvers.Select(a => new OverrideApproverDTO { Id = a.Id, Name = a.Name }).ToList();
    }

    /// <summary>Admin approves anywhere; no other role is an approver.</summary>
    private static bool IsApproverFor(User user, Guid theaterId)
    {
        return user.UserType?.Name == RoleNames.Admin;
    }

    private async Task RecordFailureAsync(Guid theaterId, Guid actorUserId, Guid approverUserId, AuditAction attemptedAction, string cause)
    {
        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = theaterId,
            ActorUserId = actorUserId,
            ApproverUserId = approverUserId,
            Action = AuditAction.OverrideFailed,
            EntityType = nameof(User),
            EntityId = approverUserId,
            DataJson = JsonSerializer.Serialize(new { attemptedAction = attemptedAction.ToString(), cause })
        });
        // Persist now (together with the counter/lockout change): the caller is about to receive a 403.
        await _uow.SaveChangesAsync();
    }
}
