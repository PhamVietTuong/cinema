using Cinema.Business.Contracts;
using Cinema.Business.DTO.Gate;
using Cinema.Data.Contracts;
using Cinema.Data.Enums;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.Managers;

public class GateManager : IGateManager
{
    private const string _admitBeforeMinutesKey = "Gate:AdmitBeforeMinutes";
    private const string _agePromptMinAgeKey = "Gate:AgePromptMinAge";
    private const int _defaultAdmitBeforeMinutes = 30;
    private const int _defaultAgePromptMinAge = 16;
    private const int _visiblePhoneDigits = 3;

    private readonly IApplicationUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly int _admitBeforeMinutes;
    private readonly int _agePromptMinAge;

    public GateManager(IApplicationUnitOfWork uow, IConfiguration config, TimeProvider clock)
    {
        _uow = uow;
        _clock = clock;
        _admitBeforeMinutes = ReadInt(config, _admitBeforeMinutesKey, _defaultAdmitBeforeMinutes);
        _agePromptMinAge = ReadInt(config, _agePromptMinAgeKey, _defaultAgePromptMinAge);
    }

    private static int ReadInt(IConfiguration config, string key, int fallback)
    {
        return int.TryParse(config[key], out var value) ? value : fallback;
    }

    public async Task<ScanTicketResultDTO> ScanAsync(Guid theaterId, Guid userId, ScanTicketRequest request)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            return new ScanTicketResultDTO { Outcome = ScanOutcome.NotFound };
        }

        var ticket = await _uow.InvoiceStore.GetGateTicketByQrAsync(code);
        if (ticket == null)
        {
            return new ScanTicketResultDTO { Outcome = ScanOutcome.NotFound };
        }

        if (ticket.InvoiceStatus != InvoiceStatus.Paid || !ticket.IsActive)
        {
            return new ScanTicketResultDTO { Outcome = ScanOutcome.NotPaid };
        }

        if (ticket.IsUsed)
        {
            return Result(ScanOutcome.AlreadyUsed, ticket);
        }

        // Another theater's ticket reveals nothing about the movie or the patron.
        if (ticket.TheaterId != theaterId)
        {
            return new ScanTicketResultDTO { Outcome = ScanOutcome.WrongTheater };
        }

        if (request.ShowTimeId.HasValue && request.ShowTimeId.Value != ticket.ShowTimeId)
        {
            return Result(ScanOutcome.WrongShowTime, ticket);
        }

        // Showtimes are stored in the theater's local time.
        var now = _clock.GetLocalNow().DateTime;
        if (now < ticket.StartTime.AddMinutes(-_admitBeforeMinutes))
        {
            return Result(ScanOutcome.TooEarly, ticket);
        }

        if (now > ticket.EndTime)
        {
            return Result(ScanOutcome.Expired, ticket);
        }

        if (ticket.MinAge >= _agePromptMinAge && !request.AgeConfirmed)
        {
            return Result(ScanOutcome.AgeCheckRequired, ticket);
        }

        var admitted = await _uow.InvoiceStore.TryAdmitTicketAsync(
            ticket.InvoiceId, ticket.SeatId, ticket.ShowTimeId, userId, _clock.GetUtcNow().UtcDateTime);
        if (!admitted)
        {
            // Lost the race against a concurrent scan of the same ticket; the winner is not known here, so
            // re-read for the "used at / by" details.
            var current = await _uow.InvoiceStore.GetGateTicketByQrAsync(code);
            return Result(ScanOutcome.AlreadyUsed, current ?? ticket);
        }

        return Result(ScanOutcome.Admitted, ticket);
    }

    public async Task<List<GateLookupResultDTO>> LookupAsync(Guid theaterId, GateLookupRequest request)
    {
        var invoiceCode = request.InvoiceCode?.Trim();
        var phone = request.Phone?.Trim();
        if (string.IsNullOrEmpty(invoiceCode) && string.IsNullOrEmpty(phone))
        {
            throw new InvalidOperationException("An invoice code or a phone number is required.");
        }

        var dayStart = _clock.GetLocalNow().DateTime.Date;
        var rows = await _uow.InvoiceStore.FindTicketsForLookupAsync(
            theaterId, invoiceCode, phone, dayStart, dayStart.AddDays(1));

        return rows
            .GroupBy(r => r.InvoiceCode)
            .Select(g => new GateLookupResultDTO
            {
                InvoiceCode = g.Key,
                CustomerName = g.First().CustomerName,
                MaskedPhone = MaskPhone(g.First().CustomerPhone),
                Tickets = g.Select(r => new GateLookupTicketDTO
                {
                    QrCode = r.QrCode ?? string.Empty,
                    SeatLabel = r.SeatLabel,
                    MovieTitle = r.MovieTitle,
                    RoomName = r.RoomName,
                    ShowTime = r.StartTime,
                    IsUsed = r.IsUsed
                }).ToList()
            })
            .ToList();
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone))
        {
            return string.Empty;
        }
        if (phone.Length <= _visiblePhoneDigits)
        {
            return phone;
        }
        return new string('*', phone.Length - _visiblePhoneDigits) + phone[^_visiblePhoneDigits..];
    }

    private static ScanTicketResultDTO Result(ScanOutcome outcome, GateTicketRow ticket)
    {
        return new ScanTicketResultDTO
        {
            Outcome = outcome,
            InvoiceCode = ticket.InvoiceCode,
            SeatLabel = ticket.SeatLabel,
            MovieTitle = ticket.MovieTitle,
            RoomName = ticket.RoomName,
            ShowTime = ticket.StartTime,
            PatronCategory = ticket.PatronCategoryName ?? string.Empty,
            AgeRatingCode = ticket.AgeRatingCode,
            MinAge = ticket.MinAge,
            UsedAt = outcome == ScanOutcome.AlreadyUsed ? ticket.UsedAt : null,
            UsedBy = outcome == ScanOutcome.AlreadyUsed ? ticket.UsedByName : null
        };
    }
}
