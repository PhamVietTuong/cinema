using System.Text.Json;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.CustomerService;
using Cinema.Business.DTO.Invoices;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Cinema.Foundation.Logging;

namespace Cinema.Business.Managers;

public class CustomerServiceManager : ICustomerServiceManager
{
    private const int _lookupInvoiceTake = 20;
    private const int _resendLimitPerHour = 3;
    private const string _pointsReference = "points";

    private readonly IApplicationUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IManagerOverrideService _overrides;
    private readonly IBoxOfficeManager _boxOffice;
    private readonly IGiftCardManager _giftCards;
    private readonly INotificationService _notifications;
    private readonly ISmsNotificationService _sms;
    private readonly TimeProvider _clock;

    public CustomerServiceManager(
        IApplicationUnitOfWork uow,
        IAuditLogger audit,
        IManagerOverrideService overrides,
        IBoxOfficeManager boxOffice,
        IGiftCardManager giftCards,
        INotificationService notifications,
        ISmsNotificationService sms,
        TimeProvider clock)
    {
        _uow = uow;
        _audit = audit;
        _overrides = overrides;
        _boxOffice = boxOffice;
        _giftCards = giftCards;
        _notifications = notifications;
        _sms = sms;
        _clock = clock;
    }

    // ── Customer lookup ──────────────────────────────────────────────────────

    public async Task<CustomerLookupDTO> LookupCustomerAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, LookupCustomerRequest request)
    {
        var query = request.Query?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            throw new InvalidOperationException("An email, a phone number or an invoice code is required.");
        }

        // One keyed lookup decides which kind of key this is: an email contains '@', otherwise try the exact phone
        // and fall back to an invoice code.
        CustomerRow? customer;
        if (query.Contains('@'))
        {
            customer = await _uow.CustomerServiceStore.FindCustomerByEmailAsync(query, RoleNames.Customer);
        }
        else
        {
            customer = await _uow.CustomerServiceStore.FindCustomerByPhoneAsync(query, RoleNames.Customer);
            if (customer == null)
            {
                customer = await _uow.CustomerServiceStore.FindCustomerByInvoiceCodeAsync(query, RoleNames.Customer);
            }
        }

        List<CustomerInvoiceRow> invoices;
        if (customer != null)
        {
            invoices = await _uow.CustomerServiceStore.GetInvoicesAsync(customer.Id, null, scopeTheaterIds, _lookupInvoiceTake);
        }
        else
        {
            // No member: a walk-in invoice code still returns that one invoice when it is in scope.
            invoices = await _uow.CustomerServiceStore.GetInvoicesAsync(null, query, scopeTheaterIds, _lookupInvoiceTake);
        }

        var result = new CustomerLookupDTO
        {
            Found = customer != null || invoices.Count > 0,
            Invoices = invoices.Select(ToDto).ToList()
        };
        if (customer != null)
        {
            result.Customer = new CustomerCardDTO
            {
                Id = customer.Id,
                Name = customer.Name,
                MaskedEmail = MaskEmail(customer.Email),
                MaskedPhone = MaskPhone(customer.Phone),
                MembershipName = customer.MembershipName,
                Points = customer.Points
            };
        }
        return result;
    }

    // ── E-ticket resend ──────────────────────────────────────────────────────

    public async Task<ResendETicketResultDTO> ResendETicketAsync(Guid theaterId, Guid staffUserId, ResendETicketRequest request)
    {
        var invoice = await _uow.CustomerServiceStore.GetResendInvoiceAsync(request.InvoiceId);
        if (invoice == null)
        {
            throw new KeyNotFoundException("Invoice not found.");
        }
        if (invoice.TheaterId != theaterId)
        {
            throw new AccessDeniedException("The invoice belongs to another theater.");
        }
        if (invoice.Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Only a paid invoice has an e-ticket to resend.");
        }

        var address = ResolveAddress(invoice, request);
        var sentInLastHour = await _uow.CustomerServiceStore.CountRecentAuditsAsync(
            invoice.Id, AuditAction.ResendTicket, _clock.GetUtcNow().UtcDateTime.AddHours(-1));
        if (sentInLastHour >= _resendLimitPerHour)
        {
            throw new InvalidOperationException($"The e-ticket was already resent {_resendLimitPerHour} times in the last hour. Try again later.");
        }

        if (request.Channel == ETicketChannel.Email)
        {
            await _notifications.SendAsync(address, $"Your e-ticket — {invoice.Code}", BuildEmailBody(invoice));
        }
        else
        {
            await _sms.SendSmsAsync(address, $"Cinema: booking {invoice.Code}. Show your e-ticket QR at the entrance.");
        }

        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = theaterId,
            ActorUserId = staffUserId,
            Action = AuditAction.ResendTicket,
            EntityType = nameof(Invoice),
            EntityId = invoice.Id,
            Amount = invoice.FinalAmount,
            DataJson = JsonSerializer.Serialize(new
            {
                InvoiceCode = invoice.Code,
                request.Channel,
                Address = request.Channel == ETicketChannel.Email ? MaskEmail(address) : MaskPhone(address)
            })
        });
        await _uow.SaveChangesAsync();

        string masked;
        if (request.Channel == ETicketChannel.Email)
        {
            masked = MaskEmail(address);
        }
        else
        {
            masked = MaskPhone(address);
        }
        return new ResendETicketResultDTO
        {
            InvoiceId = invoice.Id,
            InvoiceCode = invoice.Code,
            Channel = request.Channel,
            MaskedAddress = masked,
            RemainingThisHour = _resendLimitPerHour - sentInLastHour - 1
        };
    }

    // ── Complaints ───────────────────────────────────────────────────────────

    public async Task<ComplaintDTO> CreateComplaintAsync(Guid theaterId, Guid staffUserId, CreateComplaintRequest request)
    {
        var description = request.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            throw new InvalidOperationException("A description is required.");
        }

        var customerId = request.CustomerUserId;
        if (request.InvoiceId.HasValue)
        {
            var invoice = await _uow.CustomerServiceStore.GetInvoiceHeaderAsync(request.InvoiceId.Value);
            if (invoice == null)
            {
                throw new KeyNotFoundException("Invoice not found.");
            }
            if (invoice.TheaterId != theaterId)
            {
                throw new AccessDeniedException("The invoice belongs to another theater.");
            }
            if (!customerId.HasValue)
            {
                customerId = invoice.UserId;
            }
        }

        var complaint = new Complaint
        {
            TheaterId = theaterId,
            CustomerUserId = customerId,
            InvoiceId = request.InvoiceId,
            Category = request.Category,
            Description = description,
            CreatedByUserId = staffUserId
        };
        await _uow.ComplaintStore.CreateAsync(complaint);
        return await LoadDtoAsync(complaint.Id, complaint);
    }

    public async Task<ComplaintDTO> UpdateComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, UpdateComplaintRequest request)
    {
        var description = request.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            throw new InvalidOperationException("A description is required.");
        }

        var complaint = await LoadWorkableAsync(scopeTheaterIds, request.ComplaintId);
        complaint.Category = request.Category;
        complaint.Description = description;
        await _uow.ComplaintStore.UpdateAsync(complaint);
        return await LoadDtoAsync(complaint.Id, complaint);
    }

    public async Task<ComplaintDTO> GetComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid complaintId)
    {
        var row = await _uow.ComplaintStore.GetRowAsync(complaintId);
        if (row == null)
        {
            throw new KeyNotFoundException("Complaint not found.");
        }
        EnsureInScope(scopeTheaterIds, row.Complaint.TheaterId);
        return await ToDtoAsync(row);
    }

    public async Task<DefaultSearchResults<ComplaintDTO>> SearchComplaintsAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, PagingSearchDTO search)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var filters = search.Filters;

        var criteria = new ComplaintSearchCriteria(
            TheaterIds: scopeTheaterIds,
            Status: filters.GetEnum<ComplaintStatus>("status"),
            Category: filters.GetEnum<ComplaintCategory>("category"),
            AssignedToUserId: filters.GetGuid("assignedTo"),
            CustomerUserId: filters.GetGuid("customerId"),
            InvoiceId: filters.GetGuid("invoiceId"),
            PageIndex: page - 1,
            PageSize: pageSize);

        var (rows, total) = await _uow.ComplaintStore.SearchAsync(criteria);
        var names = await LoadNamesAsync(rows);
        return new DefaultSearchResults<ComplaintDTO>
        {
            Results = rows.Select(r => ToDto(r, names)).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<ComplaintDTO> StartComplaintReviewAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid staffUserId, StartComplaintReviewRequest request)
    {
        var complaint = await LoadWorkableAsync(scopeTheaterIds, request.ComplaintId);
        if (complaint.Status != ComplaintStatus.Open)
        {
            throw new InvalidOperationException("Only an open complaint can be taken into review.");
        }

        var assignee = staffUserId;
        if (request.AssignToUserId.HasValue)
        {
            assignee = request.AssignToUserId.Value;
        }
        if (assignee != staffUserId)
        {
            var names = await _uow.UserStore.GetNamesByIdsAsync(new[] { assignee });
            if (!names.ContainsKey(assignee))
            {
                throw new KeyNotFoundException("The assignee was not found.");
            }
        }

        complaint.Status = ComplaintStatus.InReview;
        complaint.AssignedToUserId = assignee;
        await _uow.ComplaintStore.UpdateAsync(complaint);
        return await LoadDtoAsync(complaint.Id, complaint);
    }

    public async Task<ComplaintDTO> RejectComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid staffUserId, RejectComplaintRequest request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new InvalidOperationException("A reason is required to reject a complaint.");
        }

        var complaint = await LoadWorkableAsync(scopeTheaterIds, request.ComplaintId);
        complaint.Status = ComplaintStatus.Rejected;
        complaint.ResolvedByUserId = staffUserId;
        complaint.ResolvedAt = DateTime.UtcNow;
        complaint.ResolutionNote = reason;
        await _uow.ComplaintStore.UpdateAsync(complaint);
        return await LoadDtoAsync(complaint.Id, complaint);
    }

    public async Task<ComplaintDTO> ResolveComplaintAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid staffUserId, ResolveComplaintRequest request)
    {
        var complaint = await LoadWorkableAsync(scopeTheaterIds, request.ComplaintId);
        var note = request.Note?.Trim();

        // Validate everything the chosen resolution needs BEFORE the approval: a rejected request must not burn a PIN
        // attempt, and the approval is verified before any transaction opens (a failed PIN attempt is persisted by the
        // override service and must survive).
        switch (request.Resolution)
        {
            case ComplaintResolution.Refund:
                if (!complaint.InvoiceId.HasValue)
                {
                    throw new InvalidOperationException("A refund needs the complaint to be linked to an invoice.");
                }
                break;
            case ComplaintResolution.GiftCard:
                RequireAmount(request.Amount, "gift card");
                RequireCustomer(complaint);
                break;
            case ComplaintResolution.Points:
                RequireAmount(request.Amount, "points");
                if (request.Amount!.Value != Math.Floor(request.Amount.Value))
                {
                    throw new InvalidOperationException("Points must be a whole number.");
                }
                RequireCustomer(complaint);
                break;
            case ComplaintResolution.Apology:
                break;
            default:
                throw new InvalidOperationException("Choose a resolution: Refund, GiftCard, Points or Apology.");
        }

        Guid? approverId = null;
        if (request.Resolution != ComplaintResolution.Apology)
        {
            approverId = await _overrides.VerifyAsync(complaint.TheaterId, staffUserId, request.Override, AuditAction.Compensation);
        }

        switch (request.Resolution)
        {
            case ComplaintResolution.Refund:
                await ResolveByRefundAsync(complaint, staffUserId, approverId!.Value, note, request);
                break;
            case ComplaintResolution.GiftCard:
                await ResolveByGiftCardAsync(complaint, staffUserId, approverId!.Value, note, request.Amount!.Value);
                break;
            case ComplaintResolution.Points:
                await ResolveByPointsAsync(complaint, staffUserId, approverId!.Value, note, (int)request.Amount!.Value);
                break;
            default:
                await ResolveByApologyAsync(complaint, staffUserId, note);
                break;
        }
        return await LoadDtoAsync(complaint.Id, complaint);
    }

    // ── Resolutions ──────────────────────────────────────────────────────────

    private async Task ResolveByRefundAsync(Complaint complaint, Guid staffUserId, Guid approverId, string? note, ResolveComplaintRequest request)
    {
        // The refund opens (and commits) its own transaction, so it is not wrapped. It verifies a manager approval
        // itself: an approver actor needs no PIN, anyone else forwards the override that was already accepted above.
        ManagerOverrideDTO? forwarded = request.Override;
        if (approverId == staffUserId)
        {
            forwarded = null;
        }
        var refund = await _boxOffice.StaffRefundAsync(complaint.TheaterId, staffUserId, new StaffRefundRequest
        {
            TheaterId = complaint.TheaterId,
            InvoiceId = complaint.InvoiceId!.Value,
            ReasonCode = StaffReasonCode.Compensation,
            Note = RefundNote(note),
            RefundTender = request.RefundTender,
            RefundReference = request.RefundReference,
            Override = forwarded
        });

        await _uow.BeginTransactionAsync();
        try
        {
            await FinishResolutionAsync(complaint, staffUserId, approverId, note, ComplaintResolution.Refund, refund.RefundedAmount, refund.InvoiceCode,
                new { InvoiceCode = refund.InvoiceCode, refund.RefundedAmount, refund.RefundTender, refund.OutOfBand });
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            // The refund itself is already committed and audited by the box office; log so the complaint can be closed by hand.
            await _uow.RollbackTransactionAsync();
            LogProvider.Current.Fatal($"{nameof(CustomerServiceManager)}.{nameof(ResolveByRefundAsync)}: invoice {refund.InvoiceCode} was refunded but complaint {complaint.Id} could not be closed.");
            throw;
        }
    }

    private async Task ResolveByGiftCardAsync(Complaint complaint, Guid staffUserId, Guid approverId, string? note, double amount)
    {
        var customer = await LoadCustomerAsync(complaint);
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            throw new InvalidOperationException("The customer has no email to issue the gift card to.");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            var card = await _giftCards.IssueAsync(new IssueGiftCardRequest { Amount = amount, IssuedToEmail = customer.Email });
            await FinishResolutionAsync(complaint, staffUserId, approverId, note, ComplaintResolution.GiftCard, amount, card.Code,
                new { GiftCardId = card.Id, GiftCardCode = card.Code, Amount = amount });
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task ResolveByPointsAsync(Complaint complaint, Guid staffUserId, Guid approverId, string? note, int points)
    {
        var customer = await LoadCustomerAsync(complaint);
        var before = customer.Points;

        await _uow.BeginTransactionAsync();
        try
        {
            customer.Points += points;
            var tiers = await _uow.MemberShipStore.FindAsync(m => m.MinPoints <= customer.Points);
            var tier = tiers.OrderByDescending(m => m.MinPoints).FirstOrDefault();
            if (tier != null)
            {
                customer.MemberShipId = tier.Id;
            }
            await _uow.UserStore.UpdateAsync(customer);

            await _audit.LogAsync(new AuditEntry
            {
                TheaterId = complaint.TheaterId,
                ActorUserId = staffUserId,
                ApproverUserId = approverId,
                Action = AuditAction.PointsAdjust,
                EntityType = nameof(User),
                EntityId = customer.Id,
                Amount = points,
                ReasonCode = StaffReasonCode.Compensation,
                Reason = note,
                DataJson = JsonSerializer.Serialize(new { ComplaintId = complaint.Id, Before = before, After = customer.Points })
            });
            await FinishResolutionAsync(complaint, staffUserId, approverId, note, ComplaintResolution.Points, points, _pointsReference,
                new { Points = points, Before = before, After = customer.Points });
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task ResolveByApologyAsync(Complaint complaint, Guid staffUserId, string? note)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            await FinishResolutionAsync(complaint, staffUserId, null, note, ComplaintResolution.Apology, null, null, new { });
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>Stages the Compensation audit row and saves the closed complaint inside the caller's transaction.</summary>
    private async Task FinishResolutionAsync(
        Complaint complaint, Guid staffUserId, Guid? approverId, string? note,
        ComplaintResolution resolution, double? amount, string? reference, object detail)
    {
        complaint.Status = ComplaintStatus.Resolved;
        complaint.Resolution = resolution;
        complaint.CompensationAmount = amount;
        complaint.CompensationRef = reference;
        complaint.ResolvedByUserId = staffUserId;
        complaint.ResolvedAt = DateTime.UtcNow;
        complaint.ResolutionNote = note;

        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = complaint.TheaterId,
            ActorUserId = staffUserId,
            ApproverUserId = approverId,
            Action = AuditAction.Compensation,
            EntityType = nameof(Complaint),
            EntityId = complaint.Id,
            Amount = amount,
            ReasonCode = StaffReasonCode.Compensation,
            Reason = note,
            DataJson = JsonSerializer.Serialize(new { Resolution = resolution, complaint.InvoiceId, complaint.CustomerUserId, Detail = detail })
        });
        await _uow.ComplaintStore.UpdateAsync(complaint);
        await _uow.SaveChangesAsync();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string RefundNote(string? note)
    {
        if (string.IsNullOrEmpty(note))
        {
            return "Complaint compensation";
        }
        return note;
    }

    private static void RequireAmount(double? amount, string what)
    {
        if (!amount.HasValue || amount.Value <= 0)
        {
            throw new InvalidOperationException($"A positive {what} amount is required.");
        }
    }

    private static void RequireCustomer(Complaint complaint)
    {
        if (!complaint.CustomerUserId.HasValue)
        {
            throw new InvalidOperationException("This resolution needs the complaint to be linked to a customer account.");
        }
    }

    private async Task<User> LoadCustomerAsync(Complaint complaint)
    {
        var customer = await _uow.UserStore.GetByIdAsync(complaint.CustomerUserId!.Value);
        if (customer == null)
        {
            throw new KeyNotFoundException("The customer account was not found.");
        }
        return customer;
    }

    private static void EnsureInScope(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid theaterId)
    {
        if (scopeTheaterIds != null && !scopeTheaterIds.Contains(theaterId))
        {
            throw new AccessDeniedException("The complaint is outside your scope.");
        }
    }

    /// <summary>The tracked complaint, in scope and still Open/InReview.</summary>
    private async Task<Complaint> LoadWorkableAsync(IReadOnlyCollection<Guid>? scopeTheaterIds, Guid complaintId)
    {
        var complaint = await _uow.ComplaintStore.GetByIdAsync(complaintId);
        if (complaint == null)
        {
            throw new KeyNotFoundException("Complaint not found.");
        }
        EnsureInScope(scopeTheaterIds, complaint.TheaterId);
        if (complaint.Status != ComplaintStatus.Open && complaint.Status != ComplaintStatus.InReview)
        {
            throw new InvalidOperationException("The complaint is already closed.");
        }
        return complaint;
    }

    private static string ResolveAddress(ResendInvoiceRow invoice, ResendETicketRequest request)
    {
        string? onFile;
        if (request.Channel == ETicketChannel.Email)
        {
            onFile = invoice.UserEmail;
        }
        else
        {
            onFile = invoice.UserPhone;
        }
        if (!string.IsNullOrWhiteSpace(onFile))
        {
            return onFile.Trim();
        }

        var given = request.Address?.Trim();
        if (string.IsNullOrEmpty(given))
        {
            throw new InvalidOperationException("The invoice has no customer contact: an address is required.");
        }
        if (request.Channel == ETicketChannel.Email && !given.Contains('@'))
        {
            throw new InvalidOperationException("The address is not a valid email.");
        }
        return given;
    }

    private static string BuildEmailBody(ResendInvoiceRow invoice)
    {
        var body = $"Booking code: {invoice.Code}. Total paid: {invoice.FinalAmount:0} VND. Show your e-ticket QR at the entrance.";
        if (invoice.QrCodes.Count > 0)
        {
            body += " Ticket codes: " + string.Join(", ", invoice.QrCodes) + ".";
        }
        return body;
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }
        return email[0] + "***" + email.Substring(at);
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }
        var trimmed = phone.Trim();
        if (trimmed.Length <= 3)
        {
            return "***";
        }
        return new string('*', trimmed.Length - 3) + trimmed.Substring(trimmed.Length - 3);
    }

    private static CustomerInvoiceDTO ToDto(CustomerInvoiceRow row)
    {
        return new CustomerInvoiceDTO
        {
            Id = row.Id,
            Code = row.Code,
            TheaterId = row.TheaterId,
            TheaterName = row.TheaterName,
            Status = row.Status,
            Channel = row.Channel,
            FinalAmount = row.FinalAmount,
            PaidAt = row.PaidAt,
            CreationTime = row.CreationTime,
            MovieTitle = row.MovieTitle,
            FirstShowStart = row.FirstShowStart,
            TicketCount = row.TicketCount
        };
    }

    /// <summary>Re-reads the complaint with its labels (the write above already saved it); falls back to the entity itself.</summary>
    private async Task<ComplaintDTO> LoadDtoAsync(Guid id, Complaint fallback)
    {
        var row = await _uow.ComplaintStore.GetRowAsync(id);
        if (row == null)
        {
            row = new ComplaintRow(fallback, null, null);
        }
        return await ToDtoAsync(row);
    }

    private async Task<Dictionary<Guid, string>> LoadNamesAsync(IReadOnlyCollection<ComplaintRow> rows)
    {
        var userIds = new HashSet<Guid>();
        foreach (var row in rows)
        {
            userIds.Add(row.Complaint.CreatedByUserId);
            if (row.Complaint.AssignedToUserId.HasValue)
            {
                userIds.Add(row.Complaint.AssignedToUserId.Value);
            }
            if (row.Complaint.ResolvedByUserId.HasValue)
            {
                userIds.Add(row.Complaint.ResolvedByUserId.Value);
            }
        }
        return await _uow.UserStore.GetNamesByIdsAsync(userIds.ToList());
    }

    private async Task<ComplaintDTO> ToDtoAsync(ComplaintRow row)
    {
        var names = await LoadNamesAsync(new[] { row });
        return ToDto(row, names);
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
    {
        if (id.HasValue && names.TryGetValue(id.Value, out var name))
        {
            return name;
        }
        return null;
    }

    private static ComplaintDTO ToDto(ComplaintRow row, IReadOnlyDictionary<Guid, string> names)
    {
        var c = row.Complaint;
        return new ComplaintDTO
        {
            Id = c.Id,
            TheaterId = c.TheaterId,
            CustomerUserId = c.CustomerUserId,
            CustomerName = row.CustomerName,
            InvoiceId = c.InvoiceId,
            InvoiceCode = row.InvoiceCode,
            Category = c.Category,
            Description = c.Description,
            Status = c.Status,
            Resolution = c.Resolution,
            CompensationAmount = c.CompensationAmount,
            CompensationRef = c.CompensationRef,
            AssignedToUserId = c.AssignedToUserId,
            AssignedToName = NameOf(names, c.AssignedToUserId),
            CreatedByUserId = c.CreatedByUserId,
            CreatedByName = NameOf(names, c.CreatedByUserId),
            ResolvedByUserId = c.ResolvedByUserId,
            ResolvedByName = NameOf(names, c.ResolvedByUserId),
            ResolvedAt = c.ResolvedAt,
            ResolutionNote = c.ResolutionNote,
            CreationTime = c.CreationTime
        };
    }
}
