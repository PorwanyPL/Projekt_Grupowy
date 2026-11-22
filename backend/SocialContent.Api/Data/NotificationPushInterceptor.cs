using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Data;

/// <summary>
/// Dostarcza powiadomienia w czasie rzeczywistym po UDANYM zapisie. Powiadomienia powstają w wielu miejscach
/// (NotificationService, publikacja, worker), a zapisuje je wywołujący, więc jedno miejsce nasłuchujące na SaveChanges
/// pokrywa wszystkie przypadki: przed zapisem zbiera dodane wiersze Notification, po zapisie wysyła je przez
/// <see cref="IRealtimeNotifier"/> (SignalR). Gdy zapis się nie powiedzie, nic nie jest wysyłane.
/// </summary>
public sealed class NotificationPushInterceptor(IServiceProvider services, ILogger<NotificationPushInterceptor> logger)
    : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<Notification>> _pending = new();

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        var added = context.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        if (added.Count == 0) return;
        _pending.Remove(context);
        _pending.Add(context, added);
    }

    private async Task PushAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null || !_pending.TryGetValue(context, out var list)) return;
        _pending.Remove(context);
        try
        {
            var notifier = services.GetRequiredService<IRealtimeNotifier>();
            foreach (var n in list)
                await notifier.NotificationAsync(new NotificationDto(n.Id, n.PostId, n.Message, n.CreatedAt, n.IsRead, n.TargetType, n.TargetId),
                    n.RecipientId, n.RecipientRole, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nie udało się wysłać powiadomień w czasie rzeczywistym.");
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _ = PushAsync(eventData.Context, CancellationToken.None); // synchroniczny zapis (seeder): bez blokowania
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await PushAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null) _pending.Remove(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }
}
