using System.Text.Json;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Services;

public interface IRouteCardService
{
    Task<RouteCard> SaveAsync(RouteCardSaveRequest request, int userId, CancellationToken cancellationToken);
    Task<RouteCard?> GetAsync(long id, CancellationToken cancellationToken);
}

public sealed class RouteCardService(RouteCardDbContext db) : IRouteCardService
{
    public async Task<RouteCard> SaveAsync(RouteCardSaveRequest request, int userId, CancellationToken cancellationToken)
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

        foreach (var approvalRequest in request.Approvals)
        {
            if (string.IsNullOrWhiteSpace(approvalRequest.OperationNo))
                throw new ArgumentException("Each approval must include an operation number.", nameof(request));
            var stamp = await db.UserStamps.SingleOrDefaultAsync(value => value.Id == approvalRequest.StampId && value.AppUserId == userId, cancellationToken);
            if (stamp is null)
                throw new ArgumentException("The selected stamp does not belong to the signed-in user.", nameof(request));
            db.RouteCardApprovals.Add(new RouteCardApproval
            {
                RouteCardId = card.Id,
                AppUserId = userId,
                UserStampId = stamp.Id,
                OperationNo = approvalRequest.OperationNo.Trim(),
                StampValue = stamp.StampValue,
                SignatureValue = stamp.SignatureValue,
                ApprovedAt = approvalRequest.ApprovedAt
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return card;
    }

    public Task<RouteCard?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.RouteCards.AsNoTracking().SingleOrDefaultAsync(card => card.Id == id, cancellationToken);
}
