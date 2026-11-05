using System.Text.Json;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Services;

public interface IRouteCardService
{
    Task<RouteCard> SaveAsync(RouteCardSaveRequest request, CancellationToken cancellationToken);
    Task<RouteCard?> GetAsync(long id, CancellationToken cancellationToken);
}

public sealed class RouteCardService(RouteCardDbContext db) : IRouteCardService
{
    public async Task<RouteCard> SaveAsync(RouteCardSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Card.PartNo))
            throw new ArgumentException("PartNo is required.", nameof(request));

        var card = request.Card;
        card.TraceabilityJson = JsonSerializer.Serialize(request.Traceability);
        card.OperationsJson = JsonSerializer.Serialize(request.Operations);
        card.UpdatedUtc = DateTime.UtcNow;
        db.RouteCards.Add(card);
        await db.SaveChangesAsync(cancellationToken);
        return card;
    }

    public Task<RouteCard?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.RouteCards.AsNoTracking().SingleOrDefaultAsync(card => card.Id == id, cancellationToken);
}
