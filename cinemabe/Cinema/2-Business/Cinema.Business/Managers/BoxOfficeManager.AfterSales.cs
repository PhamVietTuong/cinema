using System.Text.Json;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

/// <summary>After-sales: invoice search, staff refund, exchange, reprint and the cash drawer close/reconcile.</summary>
public partial class BoxOfficeManager
{
    private const string _staffGraceMinutesKey = "Refund:StaffGraceMinutes";
    private const string _varianceToleranceKey = "CashDrawer:VarianceTolerance";
    private const int _findInvoiceTake = 20;

    // ── Search ──────────────────────────────────────────────────────────────────

    public async Task<List<AfterSalesInvoiceDTO>> FindInvoiceAsync(Guid theaterId, FindInvoiceRequest request)
    {
        var code = request.Code?.Trim();
        var phone = request.Phone?.Trim();
        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(phone))
        {
            throw new InvalidOperationException("An invoice code or a phone number is required.");
        }

        var rows = await _uow.AfterSalesStore.FindInvoicesAsync(theaterId, code, phone, _findInvoiceTake);
        var now = BusinessCalendar.LocalNow(_clock);
        var grace = ReadDouble(_staffGraceMinutesKey, 0);
        return rows.Select(r => new AfterSalesInvoiceDTO
        {
            Id = r.Id,
            Code = r.Code,
            Status = r.Status,
            Channel = r.Channel,
            FinalAmount = r.FinalAmount,
            PaidAt = r.PaidAt,
            RefundedAt = r.RefundedAt,
            CustomerName = r.CustomerName,
            CustomerPhone = r.CustomerPhone,
            TicketCount = r.TicketCount,
            UsedTicketCount = r.UsedTicketCount,
            HasFood = r.FoodCount > 0,
            FirstShowStart = r.FirstShowStart,
            MovieTitle = r.MovieTitle,
            ExchangedFromInvoiceId = r.ExchangedFromInvoiceId,
            ShowStarted = r.FirstShowStart.HasValue && r.FirstShowStart.Value.AddMinutes(grace) <= now,
            CanRefund = r.Status == InvoiceStatus.Paid && r.UsedTicketCount == 0,
        }).ToList();
    }

    // ── Refund ──────────────────────────────────────────────────────────────────

    public async Task<StaffRefundResultDTO> StaffRefundAsync(Guid theaterId, Guid staffUserId, StaffRefundRequest request)
    {
        var note = request.Note?.Trim();
        if (request.ReasonCode == StaffReasonCode.Other && string.IsNullOrEmpty(note))
        {
            throw new InvalidOperationException("A note is required when the reason is Other.");
        }

        var invoice = await LoadInvoiceForAfterSalesAsync(theaterId, request.InvoiceId);
        EnsureRefundable(invoice);
        var showStarted = HasShowStarted(invoice);
        var isCounter = invoice.Channel == SalesChannel.Counter;
        var amount = Whole(invoice.FinalAmount);

        // Tender rules for the money going back (a counter invoice only; an online one goes back through its gateway).
        var tender = PaymentTender.Online;
        string? tenderReference = null;
        if (isCounter && amount > 0)
        {
            tender = request.RefundTender;
            if (tender != PaymentTender.Cash && tender != PaymentTender.Card && tender != PaymentTender.QrWallet)
            {
                throw new InvalidOperationException("A refund can be paid back by Cash, Card or QrWallet.");
            }
            if (tender != PaymentTender.Cash)
            {
                tenderReference = request.RefundReference?.Trim();
                if (string.IsNullOrEmpty(tenderReference))
                {
                    throw new InvalidOperationException($"A reference is required for a {tender} refund.");
                }
            }
        }

        // The approval is verified BEFORE any transaction opens: a failed PIN attempt is persisted by the override
        // service and must survive. A manager actor needs no PIN; anyone else needs a manager's PIN.
        var approverUserId = await _overrides.VerifyAsync(theaterId, staffUserId, request.Override, AuditAction.Refund);

        CashDrawerSession? drawer = null;
        if (tender == PaymentTender.Cash)
        {
            drawer = await GetOpenDrawerAsync(theaterId, staffUserId);
            if (drawer is null)
            {
                throw new InvalidOperationException("Open a cash drawer before paying a refund in cash.");
            }
            var totals = await _uow.CashDrawerStore.GetTotalsByTypeAsync(drawer.Id);
            if (totals.Values.Sum() < amount)
            {
                throw new InvalidOperationException("The drawer does not hold enough cash for this refund.");
            }
        }

        // The gateway call cannot be rolled back, so it comes after the approval and before the DB transaction. If
        // the API declines (VNPay/MoMo are returned from the merchant portal), the approved refund is recorded
        // out-of-band, exactly like the admin path of the online refund.
        var outOfBand = false;
        if (!isCounter && amount > 0)
        {
            var refund = await _gateways.Resolve(invoice.PaymentMethod).RefundAsync(invoice.PaymentReference ?? string.Empty, invoice.FinalAmount);
            outOfBand = !refund.Success;
        }

        var now = DateTime.UtcNow;
        await _uow.BeginTransactionAsync();
        try
        {
            // One conditional UPDATE decides who refunds: two simultaneous requests cannot both pay out.
            if (!await _uow.AfterSalesStore.TryClaimRefundAsync(invoice.Id, request.ReasonCode, now))
            {
                throw new InvalidOperationException("This invoice has already been refunded.");
            }
            invoice.RefundReasonCode = request.ReasonCode;
            await _booking.ReverseInvoiceEffectsAsync(invoice, staffUserId, "Refunded");

            double cashReturned = 0;
            if (drawer is not null)
            {
                cashReturned = amount;
                _uow.CashDrawerStore.StageMovement(new CashMovement
                {
                    CashDrawerSessionId = drawer.Id,
                    TheaterId = theaterId,
                    Type = CashMovementType.Refund,
                    Amount = -amount,
                    InvoiceId = invoice.Id,
                    UserId = staffUserId,
                    Note = invoice.Code,
                });
            }

            await _audit.LogAsync(new AuditEntry
            {
                TheaterId = theaterId,
                ActorUserId = staffUserId,
                ApproverUserId = approverUserId,
                Action = AuditAction.Refund,
                EntityType = nameof(Invoice),
                EntityId = invoice.Id,
                Amount = amount,
                ReasonCode = request.ReasonCode,
                Reason = note,
                DataJson = JsonSerializer.Serialize(new
                {
                    InvoiceCode = invoice.Code,
                    invoice.Channel,
                    RefundTender = tender,
                    TenderReference = tenderReference,
                    AfterShowStart = showStarted,
                    OutOfBand = outOfBand,
                }),
            });

            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();

            return new StaffRefundResultDTO
            {
                InvoiceId = invoice.Id,
                InvoiceCode = invoice.Code,
                RefundedAmount = amount,
                RefundTender = tender,
                CashReturned = cashReturned,
                RefundedAt = now,
                AfterShowStart = showStarted,
                OutOfBand = outOfBand,
            };
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    // ── Exchange ────────────────────────────────────────────────────────────────

    public async Task<ExchangeResultDTO> ExchangeAsync(Guid theaterId, Guid staffUserId, ExchangeRequest request)
    {
        var note = request.Note?.Trim();
        if (request.ReasonCode == StaffReasonCode.Other && string.IsNullOrEmpty(note))
        {
            throw new InvalidOperationException("A note is required when the reason is Other.");
        }
        if (request.RefundTender != PaymentTender.Cash && request.RefundTender != PaymentTender.Card && request.RefundTender != PaymentTender.QrWallet)
        {
            throw new InvalidOperationException("A price difference can be paid back by Cash, Card or QrWallet.");
        }
        await ValidateRequestAsync(request.NewSale);

        var oldInvoice = await LoadInvoiceForAfterSalesAsync(theaterId, request.InvoiceId);
        EnsureRefundable(oldInvoice);
        if (oldInvoice.Channel != SalesChannel.Counter)
        {
            throw new InvalidOperationException("Only a counter sale can be exchanged at the counter; refund an online booking instead.");
        }
        var oldFinal = oldInvoice.FinalAmount;

        var drawer = await GetOpenDrawerAsync(theaterId, staffUserId);
        if (request.NewSale.Tenders.Any(t => t.Method == PaymentTender.Cash) && drawer is null)
        {
            throw new InvalidOperationException("Open a cash drawer before taking cash.");
        }

        // Verified BEFORE the transaction (see StaffRefundAsync). One approval also covers any price override on the new sale.
        var approverUserId = await _overrides.VerifyAsync(theaterId, staffUserId, request.Override, AuditAction.Exchange);

        var sale = await _booking.SellAtCounterAsync(new CounterSaleContext
        {
            TheaterId = theaterId,
            StaffUserId = staffUserId,
            CashDrawerSessionId = drawer?.Id,
            ApproverUserId = approverUserId,
            Request = request.NewSale,
            ExchangedFrom = oldInvoice,
            ExchangeReasonCode = request.ReasonCode,
            ExchangeNote = note,
            ExchangeRefundTender = request.RefundTender,
        });

        var difference = Whole(sale.FinalAmount - oldFinal);
        return new ExchangeResultDTO
        {
            OldInvoiceId = oldInvoice.Id,
            OldInvoiceCode = oldInvoice.Code,
            NewSale = sale,
            AmountCollected = Math.Max(difference, 0),
            RefundedBack = Math.Max(-difference, 0),
        };
    }

    // ── Reprint ─────────────────────────────────────────────────────────────────

    public async Task<ReprintResultDTO> ReprintAsync(Guid theaterId, Guid staffUserId, ReprintRequest request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new InvalidOperationException("A reason is required to reprint tickets.");
        }

        var invoice = await LoadInvoiceForAfterSalesAsync(theaterId, request.InvoiceId);
        if (invoice.Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Only a paid invoice can be reprinted.");
        }

        // A ticket that was already used is a fraud risk: a manager must approve printing it again.
        Guid? approverUserId = null;
        var anyUsed = invoice.InvoiceTickets.Any(t => t.IsUsed);
        if (anyUsed)
        {
            approverUserId = await _overrides.VerifyAsync(theaterId, staffUserId, request.Override, AuditAction.Reprint);
        }

        var activeTickets = invoice.InvoiceTickets.Where(t => t.IsActive).ToList();
        var firstShow = activeTickets
            .Select(t => t.ShowTimeRoom?.ShowTime)
            .Where(s => s is not null)
            .OrderBy(s => s!.StartTime)
            .FirstOrDefault();
        var firstTicket = activeTickets.FirstOrDefault(t => t.ShowTimeRoom?.ShowTime == firstShow);

        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = theaterId,
            ActorUserId = staffUserId,
            ApproverUserId = approverUserId,
            Action = AuditAction.Reprint,
            EntityType = nameof(Invoice),
            EntityId = invoice.Id,
            Reason = reason,
            DataJson = JsonSerializer.Serialize(new { InvoiceCode = invoice.Code, TicketCount = activeTickets.Count, AnyTicketUsed = anyUsed }),
        });
        await _uow.SaveChangesAsync();

        return new ReprintResultDTO
        {
            InvoiceId = invoice.Id,
            InvoiceCode = invoice.Code,
            FinalAmount = invoice.FinalAmount,
            PaidAt = invoice.PaidAt,
            MovieTitle = firstShow?.Movie?.Title,
            RoomName = firstTicket?.ShowTimeRoom?.Room?.Name,
            ShowStart = firstShow?.StartTime,
            Tickets = activeTickets.Select(t => new TicketItemDTO
            {
                SeatLabel = $"{t.Seat.RowName}{t.Seat.ColIndex}",
                SeatType = t.Seat.SeatGroupId.HasValue ? "Double" : "Single",
                Price = t.Price,
                PatronCategory = t.PatronCategoryName ?? string.Empty,
                QrCode = t.QrCode,
            }).ToList(),
        };
    }

    // ── Drawer close / reconcile ────────────────────────────────────────────────

    public async Task<CloseDrawerResultDTO> CloseDrawerAsync(Guid theaterId, Guid staffUserId, CloseDrawerRequest request)
    {
        var session = await _uow.CashDrawerStore.GetByIdAsync(request.SessionId);
        if (session is null || session.TheaterId != theaterId)
        {
            throw new KeyNotFoundException("Cash drawer not found.");
        }
        if (session.UserId != staffUserId)
        {
            throw new AccessDeniedException("Only the cashier who opened a drawer can close it.");
        }
        if (session.Status != CashDrawerStatus.Open)
        {
            throw new InvalidOperationException("This drawer is already closed.");
        }
        var counted = Whole(request.CountedCash);
        if (counted < 0)
        {
            throw new InvalidOperationException("The counted cash cannot be negative.");
        }

        var totals = await _uow.CashDrawerStore.GetTotalsByTypeAsync(session.Id);
        var expected = Whole(totals.Values.Sum());
        var variance = counted - expected;
        var needsReconciliation = Math.Abs(variance) > ReadDouble(_varianceToleranceKey, 0);

        session.ClosedAt = DateTime.UtcNow;
        session.CountedCash = counted;
        session.ExpectedCash = expected;
        session.Variance = variance;
        session.Status = needsReconciliation ? CashDrawerStatus.Closed : CashDrawerStatus.Reconciled;
        await _uow.CashDrawerStore.UpdateAsync(session);

        if (needsReconciliation)
        {
            await _audit.LogAsync(new AuditEntry
            {
                TheaterId = theaterId,
                ActorUserId = staffUserId,
                Action = AuditAction.Other,
                EntityType = nameof(CashDrawerSession),
                EntityId = session.Id,
                Amount = variance,
                Reason = request.Note?.Trim(),
                DataJson = JsonSerializer.Serialize(new { Event = "DrawerClosedWithVariance", Expected = expected, Counted = counted }),
            });
        }
        await _uow.SaveChangesAsync();

        return ToCloseResult(session);
    }

    public async Task<CloseDrawerResultDTO> ReconcileDrawerAsync(Guid theaterId, Guid staffUserId, ReconcileDrawerRequest request)
    {
        var session = await _uow.CashDrawerStore.GetByIdAsync(request.SessionId);
        if (session is null || session.TheaterId != theaterId)
        {
            throw new KeyNotFoundException("Cash drawer not found.");
        }
        if (session.Status == CashDrawerStatus.Open)
        {
            throw new InvalidOperationException("The cashier must close this drawer first.");
        }
        if (session.Status == CashDrawerStatus.Reconciled)
        {
            throw new InvalidOperationException("This drawer is already reconciled.");
        }

        var approverUserId = await _overrides.VerifyAsync(theaterId, staffUserId, request.Override, AuditAction.DrawerReconcile);

        session.Status = CashDrawerStatus.Reconciled;
        await _uow.CashDrawerStore.UpdateAsync(session);
        await _audit.LogAsync(new AuditEntry
        {
            TheaterId = theaterId,
            ActorUserId = staffUserId,
            ApproverUserId = approverUserId,
            Action = AuditAction.DrawerReconcile,
            EntityType = nameof(CashDrawerSession),
            EntityId = session.Id,
            Amount = session.Variance,
            Reason = request.Note.Trim(),
            DataJson = JsonSerializer.Serialize(new { Expected = session.ExpectedCash, Counted = session.CountedCash }),
        });
        await _uow.SaveChangesAsync();

        return ToCloseResult(session);
    }

    private static CloseDrawerResultDTO ToCloseResult(CashDrawerSession session)
    {
        return new CloseDrawerResultDTO
        {
            SessionId = session.Id,
            Status = session.Status,
            ExpectedCash = session.ExpectedCash ?? 0,
            CountedCash = session.CountedCash ?? 0,
            Variance = session.Variance ?? 0,
            NeedsReconciliation = session.Status == CashDrawerStatus.Closed,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<Invoice> LoadInvoiceForAfterSalesAsync(Guid theaterId, Guid invoiceId)
    {
        var invoice = await _uow.InvoiceStore.GetWithDetailsAsync(invoiceId);
        // Another theater's invoice is reported as not found, so it reveals nothing.
        if (invoice is null || invoice.TheaterId != theaterId)
        {
            throw new KeyNotFoundException("Invoice not found.");
        }
        return invoice;
    }

    private static void EnsureRefundable(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Only a paid invoice can be refunded or exchanged.");
        }
        if (invoice.InvoiceTickets.Any(t => t.IsUsed))
        {
            throw new InvalidOperationException("A ticket of this invoice has already been used.");
        }
    }

    /// <summary>The earliest showtime of the invoice started more than <c>Refund:StaffGraceMinutes</c> ago (theater local time).</summary>
    private bool HasShowStarted(Invoice invoice)
    {
        var earliestStart = invoice.InvoiceTickets
            .Select(t => t.ShowTimeRoom?.ShowTime?.StartTime)
            .Where(s => s.HasValue)
            .Min();
        if (!earliestStart.HasValue)
        {
            return false;
        }
        return earliestStart.Value.AddMinutes(ReadDouble(_staffGraceMinutesKey, 0)) <= BusinessCalendar.LocalNow(_clock);
    }

    private async Task<CashDrawerSession?> GetOpenDrawerAsync(Guid theaterId, Guid staffUserId)
    {
        var drawer = await _uow.CashDrawerStore.GetOpenForUserAsync(staffUserId);
        return drawer is not null && drawer.TheaterId == theaterId ? drawer : null;
    }

    private double ReadDouble(string key, double fallback)
    {
        return double.TryParse(_config[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
