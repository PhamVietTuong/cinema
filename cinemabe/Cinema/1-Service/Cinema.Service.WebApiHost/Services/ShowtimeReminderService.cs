using Cinema.Business.Contracts;
using Cinema.Data.Contracts;
using Cinema.Foundation.Logging;

namespace Cinema.Service.WebApiHost.Services;

/// <summary>
/// Periodically emails a "your showtime is soon" reminder to customers holding paid tickets
/// for a showtime starting ~1 hour out. Delivery goes through <see cref="INotificationService"/>
/// (real email when SMTP is configured, dev log otherwise). Dedup is in-memory per (user, showtime).
/// </summary>
public class ShowtimeReminderService : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _lead     = TimeSpan.FromMinutes(60); // remind ~1h before
    private static readonly TimeSpan _window    = TimeSpan.FromMinutes(15); // scan band each tick

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HashSet<string> _sent = new(); // "{userId}:{showTimeId}" already reminded

    public ShowtimeReminderService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

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
                    var key = $"{group.Key.UserId}:{group.Key.ShowTimeId}";
                    if (!_sent.Add(key)) { continue; } // already reminded this run's lifetime

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
