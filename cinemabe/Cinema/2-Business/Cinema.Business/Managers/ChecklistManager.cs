using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Operations;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class ChecklistManager : IChecklistManager
{
    private const int _maxItems = 50;

    private readonly IApplicationUnitOfWork _uow;

    public ChecklistManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<ChecklistTemplateDTO>> GetTemplatesAsync(Guid theaterId)
    {
        var templates = await _uow.ChecklistStore.GetTemplatesAsync(theaterId);
        return templates.Select(t => ToDto(t, t.Items)).ToList();
    }

    public async Task<ChecklistTemplateDTO> SaveTemplateAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId, SaveChecklistTemplateRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("The template needs a name.");
        }
        if (request.Items.Count == 0 || request.Items.Count > _maxItems)
        {
            throw new InvalidOperationException($"A template needs between 1 and {_maxItems} items.");
        }
        if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.Text)))
        {
            throw new InvalidOperationException("Every checklist item needs a text.");
        }

        ChecklistTemplate template;
        if (request.Id.HasValue)
        {
            var existing = await _uow.ChecklistStore.GetTemplateAsync(request.Id.Value);
            if (existing == null)
            {
                throw new KeyNotFoundException("Checklist template not found.");
            }
            EnsureInScope(scopeTheaterIds, existing.TheaterId);
            template = existing;
        }
        else
        {
            template = new ChecklistTemplate { TheaterId = theaterId };
        }

        if (request.IsActive && await _uow.ChecklistStore.HasActiveTemplateAsync(template.TheaterId, request.Kind, request.Id))
        {
            throw new InvalidOperationException("The theater already has an active checklist of this kind. Deactivate it first.");
        }

        template.Name = name;
        template.Kind = request.Kind;
        template.IsActive = request.IsActive;
        var items = request.Items
            .Select((item, index) => new ChecklistTemplateItem
            {
                ChecklistTemplateId = template.Id,
                SortOrder = index,
                Text = item.Text.Trim(),
                IsRequired = item.IsRequired
            })
            .ToList();

        if (request.Id.HasValue)
        {
            template.LastUpdatedTime = DateTime.UtcNow;
            _uow.ChecklistStore.StageReplaceTemplateItems(template, items);
        }
        else
        {
            foreach (var item in items)
            {
                template.Items.Add(item);
            }
            _uow.ChecklistStore.StageTemplate(template);
        }
        await _uow.SaveChangesAsync();
        return ToDto(template, items);
    }

    public async Task<ChecklistRunDTO> OpenAsync(Guid theaterId, OpenChecklistRequest request)
    {
        var showTimeRoom = await _uow.ShowTimeStore.GetShowTimeRoomAsync(request.ShowTimeId, request.RoomId);
        if (showTimeRoom == null)
        {
            throw new KeyNotFoundException("Showtime not found in that room.");
        }
        if (showTimeRoom.Room.TheaterId != theaterId)
        {
            throw new AccessDeniedException("The showtime belongs to another theater.");
        }

        var run = await _uow.ChecklistStore.GetRunAsync(request.ShowTimeId, request.RoomId, request.Kind);
        if (run == null)
        {
            var template = await _uow.ChecklistStore.GetActiveTemplateAsync(theaterId, request.Kind);
            if (template == null)
            {
                throw new KeyNotFoundException("This theater has no active checklist of that kind.");
            }

            run = new ChecklistRun
            {
                TheaterId = theaterId,
                ShowTimeId = request.ShowTimeId,
                RoomId = request.RoomId,
                Kind = request.Kind,
                ChecklistTemplateId = template.Id,
                TemplateName = template.Name
            };
            foreach (var item in template.Items.OrderBy(i => i.SortOrder))
            {
                run.Items.Add(new ChecklistRunItem
                {
                    ChecklistRunId = run.Id,
                    SortOrder = item.SortOrder,
                    Text = item.Text,
                    IsRequired = item.IsRequired
                });
            }

            try
            {
                await _uow.ChecklistStore.AddRunAsync(run);
            }
            catch (Exception)
            {
                // Two people opened it at once: the unique (showtime, room, kind) index let only one create it.
                var existing = await _uow.ChecklistStore.GetRunAsync(request.ShowTimeId, request.RoomId, request.Kind);
                if (existing == null)
                {
                    throw;
                }
                run = existing;
            }
        }
        return await ToDtoAsync(run);
    }

    public async Task<ChecklistRunDTO> SetItemAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid actorUserId, SetChecklistItemRequest request)
    {
        var run = await _uow.ChecklistStore.GetRunByItemAsync(request.RunItemId);
        if (run == null)
        {
            throw new KeyNotFoundException("Checklist item not found.");
        }
        EnsureInScope(scopeTheaterIds, run.TheaterId);
        if (run.CompletedAt.HasValue)
        {
            throw new InvalidOperationException("The checklist is already completed.");
        }

        var item = run.Items.First(i => i.Id == request.RunItemId);
        item.IsDone = request.IsDone;
        item.Note = request.Note?.Trim();
        item.LastUpdatedTime = DateTime.UtcNow;
        if (request.IsDone)
        {
            item.DoneByUserId = actorUserId;
            item.DoneAt = DateTime.UtcNow;
        }
        else
        {
            item.DoneByUserId = null;
            item.DoneAt = null;
        }
        await _uow.SaveChangesAsync();
        return await ToDtoAsync(run);
    }

    public async Task<ChecklistRunDTO> CompleteAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid actorUserId, CompleteChecklistRequest request)
    {
        var run = await _uow.ChecklistStore.GetRunByIdAsync(request.RunId);
        if (run == null)
        {
            throw new KeyNotFoundException("Checklist not found.");
        }
        EnsureInScope(scopeTheaterIds, run.TheaterId);
        if (run.CompletedAt.HasValue)
        {
            throw new InvalidOperationException("The checklist is already completed.");
        }

        var missing = run.Items.Count(i => i.IsRequired && !i.IsDone);
        if (missing > 0)
        {
            throw new InvalidOperationException($"{missing} required item(s) are not done yet.");
        }

        run.CompletedAt = DateTime.UtcNow;
        run.CompletedByUserId = actorUserId;
        run.LastUpdatedTime = DateTime.UtcNow;
        await _uow.SaveChangesAsync();
        return await ToDtoAsync(run);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void EnsureInScope(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId)
    {
        if (scopeTheaterIds != null && !scopeTheaterIds.Contains(theaterId))
        {
            throw new AccessDeniedException("The checklist is outside your scope.");
        }
    }

    private static ChecklistTemplateDTO ToDto(ChecklistTemplate template, IEnumerable<ChecklistTemplateItem> items)
    {
        return new ChecklistTemplateDTO
        {
            Id = template.Id,
            TheaterId = template.TheaterId,
            Name = template.Name,
            Kind = template.Kind,
            IsActive = template.IsActive,
            Items = items.OrderBy(i => i.SortOrder)
                .Select(i => new ChecklistTemplateItemDTO { Id = i.Id, Text = i.Text, IsRequired = i.IsRequired })
                .ToList()
        };
    }

    private async Task<ChecklistRunDTO> ToDtoAsync(ChecklistRun run)
    {
        var userIds = run.Items.Where(i => i.DoneByUserId.HasValue).Select(i => i.DoneByUserId!.Value).Distinct().ToList();
        var names = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            names = await _uow.UserStore.GetNamesByIdsAsync(userIds);
        }

        return new ChecklistRunDTO
        {
            Id = run.Id,
            TheaterId = run.TheaterId,
            ShowTimeId = run.ShowTimeId,
            RoomId = run.RoomId,
            Kind = run.Kind,
            TemplateName = run.TemplateName,
            CompletedAt = run.CompletedAt,
            CompletedByUserId = run.CompletedByUserId,
            Items = run.Items.OrderBy(i => i.SortOrder).Select(i =>
            {
                string? doneByName = null;
                if (i.DoneByUserId.HasValue && names.TryGetValue(i.DoneByUserId.Value, out var name))
                {
                    doneByName = name;
                }
                return new ChecklistRunItemDTO
                {
                    Id = i.Id,
                    SortOrder = i.SortOrder,
                    Text = i.Text,
                    IsRequired = i.IsRequired,
                    IsDone = i.IsDone,
                    DoneByUserId = i.DoneByUserId,
                    DoneByName = doneByName,
                    DoneAt = i.DoneAt,
                    Note = i.Note
                };
            }).ToList()
        };
    }
}
