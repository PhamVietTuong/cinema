using Cinema.Business.Contracts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Service.WebApiHost.Services;

/// <summary>
/// Periodically emails a "your showtime is soon" reminder to customers holding paid tickets
/// for a showtime starting ~1 hour out. Delivery goes through <see cref="INotificationService"/>
/// (real email when SMTP is configured, dev log otherwise). Dedup is persisted in ReminderLog so a
/// process restart doesn't re-send, and the unique (user, showtime) index dedups across instances.
/// </summary>
public class ShowtimeReminderService : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _lead     = TimeSpan.FromMinutes(60); // remind ~1h before
    private static readonly TimeSpan _window    = TimeSpan.FromMinutes(15); // scan band each tick

    private readonly IServiceScopeFactory _scopeFactory;

    public ShowtimeReminderService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow    = scope.ServiceProvider.GetRequiredService<IApplicationUnitOfWork>();
                var notify = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var from = DateTime.Now.Add(_lead);
                var to   = from.Add(_window);
                var tickets = await uow.InvoiceStore.GetPaidTicketsForShowtimesAsync(from, to);

                foreach (var group in tickets.GroupBy(t => new { t.Invoice.UserId, t.ShowTimeRoom.ShowTimeId }))
                {
                    var userId     = group.Key.UserId;
                    var showTimeId = group.Key.ShowTimeId;
                    // Persisted dedup: survives restarts, and the unique index dedups across instances.
                    if (await uow.ReminderLogStore.WasSentAsync(userId, showTimeId)) { continue; }

                    var first = group.First();
                    var email = first.Invoice.User?.Email;
                    if (string.IsNullOrWhiteSpace(email)) { continue; }
                    // Respect the customer's opt-out of showtime reminder emails.
                    if (first.Invoice.User?.NotifyReminderEmails == false) { continue; }

                    var movie = first.ShowTimeRoom?.ShowTime?.Movie?.Title ?? "phim";
                    var start = first.ShowTimeRoom?.ShowTime?.StartTime ?? default;
                    var seats = string.Join(", ", group
                        .Where(t => t.Seat != null)
                        .Select(t => $"{t.Seat!.RowName}{t.Seat.ColIndex}"));

                    await notify.SendAsync(email!,
                        $"Nhắc lịch: {movie} lúc {start:HH:mm dd/MM}",
                        $"Suất chiếu \"{movie}\" bắt đầu lúc {start:HH:mm dd/MM}. Ghế: {seats}. Hẹn gặp bạn tại rạp!");

                    // Record the send so a restart (or another instance) won't repeat it.
                    try
                    {
                        await uow.ReminderLogStore.CreateAsync(new ReminderLog
                        {
                            UserId = userId, ShowTimeId = showTimeId, SentAt = DateTime.UtcNow,
                        });
                    }
                    catch (DbUpdateException)
                    {
                        // Another instance recorded this reminder first — the unique index rejected it. Fine.
                    }
                }
            }
            catch (Exception e)
            {
                LogProvider.Current.Fatal(e, $"ShowtimeReminderService->Exception: {e.GetType()}, {e.Message}");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
