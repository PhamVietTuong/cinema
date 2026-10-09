using System.Text.Json;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Operations;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Business.Notifications;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Managers;

public class IncidentManager : IIncidentManager
{
    private readonly IApplicationUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IManagerOverrideService _overrides;

    private readonly IStaffNotificationService _staffNotifications;

    public IncidentManager(IApplicationUnitOfWork uow, IAuditLogger audit, IManagerOverrideService overrides, IStaffNotificationService? staffNotifications = null)
    {
        _uow = uow;
        _audit = audit;
        _overrides = overrides;
        _staffNotifications = staffNotifications ?? new NoOpStaffNotificationService();
    }

    public async Task<IncidentDTO> ReportAsync(Guid theaterId, Guid actorUserId, ReportIncidentRequest request)
    {
        Room? room = null;
        Seat? seat = null;
        if (request.SeatId.HasValue)
        {
            seat = await LoadSeatAsync(request.SeatId.Value);
            if (request.RoomId.HasValue && request.RoomId.Value != seat.RoomId)
            {
                throw new InvalidOperationException("The seat does not belong to the given room.");
            }
            room = await LoadRoomInTheaterAsync(theaterId, seat.RoomId);
        }
        else if (request.RoomId.HasValue)
        {
            room = await LoadRoomInTheaterAsync(theaterId, request.RoomId.Value);
        }

        var incident = new Incident
        {
            TheaterId = theaterId,
            RoomId = room?.Id,
            SeatId = seat?.Id,
            ShowTimeId = request.ShowTimeId,
            Category = request.Category,
            Severity = request.Severity,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            ReportedByUserId = actorUserId
        };
        await _uow.IncidentStore.CreateAsync(incident);

        var dto = await ToDtoAsync(new IncidentRow(incident, room?.Name, seat?.RowName, seat?.ColIndex));
        try
        {
            await _staffNotifications.NotifyIncidentRaisedAsync(theaterId, dto);
        }
        catch (Exception e)
        {
            // The incident is saved; a failed push must not fail the reporter's request.
            LogProvider.Current.Warning(e, $"{nameof(IncidentManager)}.{nameof(ReportAsync)} push failed for incident {incident.Id}: {e.Message}");
        }
        return dto;
    }

    public async Task<IncidentDTO> ResolveAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid actorUserId, ResolveIncidentRequest request)
    {
        var incident = await _uow.IncidentStore.GetByIdAsync(request.IncidentId);
        if (incident == null)
        {
            throw new KeyNotFoundException("Incident not found.");
        }
        EnsureInScope(scopeTheaterIds, incident.TheaterId);
        // A resolved incident may still be holding a seat/room block (it was resolved without unblocking):
        // resolving again with Unblock releases it. Otherwise a resolved incident is final.
        var alreadyResolved = incident.Status == IncidentStatus.Resolved;
        var stillBlocking = incident.BlocksSeat || incident.BlocksRoom;
        if (alreadyResolved && !(request.Unblock && stillBlocking))
        {
            throw new InvalidOperationException("The incident is already resolved.");
        }

        var unblockSeat = request.Unblock && incident.BlocksSeat && incident.SeatId.HasValue;
        var unblockRoom = request.Unblock && incident.BlocksRoom && incident.RoomId.HasValue;

        List<Seat> seatsToOpen = new();
        Room? roomToOpen = null;
        Guid? approverId = null;
        if (unblockSeat)
        {
            var seat = await LoadSeatAsync(incident.SeatId!.Value);
            seatsToOpen = await LoadSeatGroupAsync(seat);
            approverId = await _overrides.VerifyAsync(incident.TheaterId, actorUserId, request.Override, AuditAction.BlockSeat);
        }
        if (unblockRoom)
        {
            roomToOpen = await LoadRoomInTheaterAsync(incident.TheaterId, incident.RoomId!.Value);
            approverId = await _overrides.VerifyAsync(incident.TheaterId, actorUserId, request.Override, AuditAction.BlockRoom);
        }

        await _uow.BeginTransactionAsync();
        try
        {
            if (unblockSeat)
            {
                foreach (var seat in seatsToOpen)
                {
                    seat.IsActive = true;
                    await _uow.SeatStore.UpdateAsync(seat);
                }
                incident.BlocksSeat = false;
                await _audit.LogAsync(BuildAudit(incident, approverId, actorUserId, AuditAction.BlockSeat, nameof(Seat), incident.SeatId,
                    new { incidentId = incident.Id, unblock = true, seatIds = seatsToOpen.Select(s => s.Id).ToList() }));
            }
            if (unblockRoom && roomToOpen != null)
            {
                if (roomToOpen.Status == RoomStatus.Maintenance)
                {
                    roomToOpen.Status = RoomStatus.Active;
                    await _uow.RoomStore.UpdateAsync(roomToOpen);
                }
                incident.BlocksRoom = false;
                await _audit.LogAsync(BuildAudit(incident, approverId, actorUserId, AuditAction.BlockRoom, nameof(Room), incident.RoomId,
                    new { incidentId = incident.Id, unblock = true }));
            }

            if (!alreadyResolved)
            {
                incident.Status = IncidentStatus.Resolved;
                incident.ResolvedByUserId = actorUserId;
                incident.ResolvedAt = DateTime.UtcNow;
                incident.ResolutionNote = request.ResolutionNote?.Trim();
            }
            await _uow.IncidentStore.UpdateAsync(incident);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        var row = await _uow.IncidentStore.GetRowAsync(incident.Id);
        if (row == null)
        {
            row = new IncidentRow(incident, null, null, null);
        }
        return await ToDtoAsync(row);
    }

    public async Task<IncidentDTO> GetAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid incidentId)
    {
        var row = await _uow.IncidentStore.GetRowAsync(incidentId);
        if (row == null)
        {
            throw new KeyNotFoundException("Incident not found.");
        }
        EnsureInScope(scopeTheaterIds, row.Incident.TheaterId);
        return await ToDtoAsync(row);
    }

    public async Task<DefaultSearchResults<IncidentDTO>> SearchAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var filters = search.Filters;

        var to = filters.GetDateTime("to");
        if (to.HasValue && to.Value.TimeOfDay == TimeSpan.Zero)
        {
            // A bare date means "through the end of that day" (SQL datetime resolution is ~3 ms).
            to = to.Value.AddDays(1).AddMilliseconds(-3);
        }

        var criteria = new IncidentSearchCriteria(
            TheaterIds: scopeTheaterIds,
            Status: filters.GetEnum<IncidentStatus>("status"),
            Category: filters.GetEnum<IncidentCategory>("category"),
            From: filters.GetDateTime("from"),
            To: to,
            PageIndex: page - 1,
            PageSize: pageSize);

        var (rows, total) = await _uow.IncidentStore.SearchAsync(criteria);
        var names = await LoadNamesAsync(rows);
        return new DefaultSearchResults<IncidentDTO>
        {
            Results = rows.Select(r => ToDto(r, names)).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<BlockResultDTO> BlockSeatAsync(Guid theaterId, Guid actorUserId, BlockSeatRequest request)
    {
        var seat = await LoadSeatAsync(request.SeatId);
        var room = await LoadRoomInTheaterAsync(theaterId, seat.RoomId);
        var incident = await LoadOpenIncidentAsync(theaterId, request.IncidentId);

        var seats = await LoadSeatGroupAsync(seat);
        if (seats.All(s => !s.IsActive))
        {
            throw new InvalidOperationException("The seat is already blocked.");
        }

        var approverId = await _overrides.VerifyAsync(theaterId, actorUserId, request.Override, AuditAction.BlockSeat);

        var seatIds = seats.Select(s => s.Id).ToList();
        var affected = await _uow.IncidentStore.GetUpcomingTicketsAsync(room.Id, seatIds, DateTime.Now);

        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var blocked in seats)
            {
                blocked.IsActive = false;
                await _uow.SeatStore.UpdateAsync(blocked);
            }

            if (incident == null)
            {
                incident = new Incident
                {
                    TheaterId = theaterId,
                    Category = IncidentCategory.Seat,
                    Severity = IncidentSeverity.High,
                    Title = TitleOrDefault(request.Title, $"Seat {seat.RowName}{seat.ColIndex} blocked"),
                    Description = request.Description?.Trim(),
                    ReportedByUserId = actorUserId
                };
                ApplySeatBlock(incident, room.Id, seat.Id);
                await _uow.IncidentStore.CreateAsync(incident);
            }
            else
            {
                ApplySeatBlock(incident, room.Id, seat.Id);
                await _uow.IncidentStore.UpdateAsync(incident);
            }

            await _audit.LogAsync(BuildAudit(incident, approverId, actorUserId, AuditAction.BlockSeat, nameof(Seat), seat.Id,
                new { incidentId = incident.Id, seatIds, affectedTickets = affected.Count }));
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return new BlockResultDTO
        {
            IncidentId = incident.Id,
            BlockedSeatIds = seatIds,
            AffectedTickets = affected.Select(ToDto).ToList()
        };
    }

    public async Task<BlockResultDTO> BlockRoomAsync(Guid theaterId, Guid actorUserId, BlockRoomRequest request)
    {
        var room = await LoadRoomInTheaterAsync(theaterId, request.RoomId);
        var incident = await LoadOpenIncidentAsync(theaterId, request.IncidentId);
        if (room.Status != RoomStatus.Active)
        {
            throw new InvalidOperationException("Only a room that is open for sale can be blocked.");
        }

        var approverId = await _overrides.VerifyAsync(theaterId, actorUserId, request.Override, AuditAction.BlockRoom);

        var affected = await _uow.IncidentStore.GetUpcomingTicketsAsync(room.Id, null, DateTime.Now);

        await _uow.BeginTransactionAsync();
        try
        {
            room.Status = RoomStatus.Maintenance;
            await _uow.RoomStore.UpdateAsync(room);

            if (incident == null)
            {
                incident = new Incident
                {
                    TheaterId = theaterId,
                    Category = IncidentCategory.Room,
                    Severity = IncidentSeverity.High,
                    Title = TitleOrDefault(request.Title, $"{room.Name} blocked for maintenance"),
                    Description = request.Description?.Trim(),
                    ReportedByUserId = actorUserId,
                    RoomId = room.Id,
                    BlocksRoom = true
                };
                await _uow.IncidentStore.CreateAsync(incident);
            }
            else
            {
                incident.RoomId = room.Id;
                incident.BlocksRoom = true;
                await _uow.IncidentStore.UpdateAsync(incident);
            }

            await _audit.LogAsync(BuildAudit(incident, approverId, actorUserId, AuditAction.BlockRoom, nameof(Room), room.Id,
                new { incidentId = incident.Id, affectedTickets = affected.Count }));
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return new BlockResultDTO
        {
            IncidentId = incident.Id,
            BlockedRoomId = room.Id,
            AffectedTickets = affected.Select(ToDto).ToList()
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void ApplySeatBlock(Incident incident, Guid roomId, Guid seatId)
    {
        incident.RoomId = roomId;
        incident.SeatId = seatId;
        incident.BlocksSeat = true;
    }

    private static string TitleOrDefault(string? title, string fallback)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return fallback;
        }
        return title.Trim();
    }

    private static void EnsureInScope(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId)
    {
        if (scopeTheaterIds != null && !scopeTheaterIds.Contains(theaterId))
        {
            throw new AccessDeniedException("The incident is outside your scope.");
        }
    }

    private async Task<Seat> LoadSeatAsync(Guid seatId)
    {
        var seat = await _uow.SeatStore.GetByIdAsync(seatId);
        if (seat == null)
        {
            throw new KeyNotFoundException("Seat not found.");
        }
        return seat;
    }

    /// <summary>The seat plus its double-seat partner(s): they are sold together, so they are blocked together.</summary>
    private async Task<List<Seat>> LoadSeatGroupAsync(Seat seat)
    {
        if (!seat.SeatGroupId.HasValue)
        {
            return new List<Seat> { seat };
        }
        var groupId = seat.SeatGroupId.Value;
        var group = (await _uow.SeatStore.FindAllAsync(s => s.SeatGroupId == groupId)).ToList();
        if (group.All(s => s.Id != seat.Id))
        {
            group.Add(seat);
        }
        return group;
    }

    private async Task<Room> LoadRoomInTheaterAsync(Guid theaterId, Guid roomId)
    {
        var room = await _uow.RoomStore.GetByIdAsync(roomId);
        if (room == null)
        {
            throw new KeyNotFoundException("Room not found.");
        }
        if (room.TheaterId != theaterId)
        {
            throw new AccessDeniedException("The room belongs to another theater.");
        }
        return room;
    }

    private async Task<Incident?> LoadOpenIncidentAsync(Guid theaterId, Guid? incidentId)
    {
        if (!incidentId.HasValue)
        {
            return null;
        }
        var incident = await _uow.IncidentStore.GetByIdAsync(incidentId.Value);
        if (incident == null)
        {
            throw new KeyNotFoundException("Incident not found.");
        }
        if (incident.TheaterId != theaterId)
        {
            throw new AccessDeniedException("The incident belongs to another theater.");
        }
        if (incident.Status != IncidentStatus.Open)
        {
            throw new InvalidOperationException("The incident is already resolved.");
        }
        return incident;
    }

    private static AuditEntry BuildAudit(Incident incident, Guid? approverId, Guid actorUserId, AuditAction action, string entityType, Guid? entityId, object data)
    {
        return new AuditEntry
        {
            TheaterId = incident.TheaterId,
            ActorUserId = actorUserId,
            ApproverUserId = approverId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ReasonCode = StaffReasonCode.TechnicalIssue,
            Reason = incident.Title,
            DataJson = JsonSerializer.Serialize(data)
        };
    }

    private async Task<Dictionary<Guid, string>> LoadNamesAsync(IReadOnlyCollection<IncidentRow> rows)
    {
        var userIds = rows.Select(r => r.Incident.ReportedByUserId)
            .Concat(rows.Where(r => r.Incident.ResolvedByUserId.HasValue).Select(r => r.Incident.ResolvedByUserId!.Value))
            .Distinct()
            .ToList();
        return await _uow.UserStore.GetNamesByIdsAsync(userIds);
    }

    private async Task<IncidentDTO> ToDtoAsync(IncidentRow row)
    {
        var names = await LoadNamesAsync(new[] { row });
        return ToDto(row, names);
    }

    private static IncidentDTO ToDto(IncidentRow row, IReadOnlyDictionary<Guid, string> names)
    {
        var incident = row.Incident;
        string? seatLabel = null;
        if (row.SeatRowName != null)
        {
            seatLabel = $"{row.SeatRowName}{row.SeatColIndex}";
        }
        names.TryGetValue(incident.ReportedByUserId, out var reporterName);
        string? resolverName = null;
        if (incident.ResolvedByUserId.HasValue && names.TryGetValue(incident.ResolvedByUserId.Value, out var name))
        {
            resolverName = name;
        }

        return new IncidentDTO
        {
            Id = incident.Id,
            TheaterId = incident.TheaterId,
            RoomId = incident.RoomId,
            RoomName = row.RoomName,
            SeatId = incident.SeatId,
            SeatLabel = seatLabel,
            ShowTimeId = incident.ShowTimeId,
            Category = incident.Category,
            Severity = incident.Severity,
            Status = incident.Status,
            Title = incident.Title,
            Description = incident.Description,
            ReportedByUserId = incident.ReportedByUserId,
            ReportedByName = reporterName,
            ResolvedByUserId = incident.ResolvedByUserId,
            ResolvedByName = resolverName,
            ResolvedAt = incident.ResolvedAt,
            ResolutionNote = incident.ResolutionNote,
            BlocksSeat = incident.BlocksSeat,
            BlocksRoom = incident.BlocksRoom,
            CreationTime = incident.CreationTime
        };
    }

    private static AffectedTicketDTO ToDto(AffectedTicketRow row)
    {
        return new AffectedTicketDTO
        {
            InvoiceId = row.InvoiceId,
            InvoiceCode = row.InvoiceCode,
            CustomerName = row.CustomerName,
            CustomerPhone = row.CustomerPhone,
            ShowTimeId = row.ShowTimeId,
            RoomId = row.RoomId,
            RoomName = row.RoomName,
            MovieTitle = row.MovieTitle,
            StartTime = row.StartTime,
            SeatId = row.SeatId,
            SeatLabel = $"{row.SeatRowName}{row.SeatColIndex}"
        };
    }
}
