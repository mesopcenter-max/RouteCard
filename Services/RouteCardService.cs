using System.Text.Json;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Services;

public interface IRouteCardService
{
    Task<RouteCard> SaveAsync(RouteCardSaveRequest request, int userId, int? serialNumber, CancellationToken cancellationToken);
    Task<RouteCard?> GetAsync(long id, CancellationToken cancellationToken);
}

public sealed class RouteCardService(RouteCardDbContext db) : IRouteCardService
{
    public async Task<RouteCard> SaveAsync(RouteCardSaveRequest request, int userId, int? serialNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Card.PartNo))
            throw new ArgumentException("PartNo is required.", nameof(request));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var card = request.Card;
        card.TraceabilityJson = JsonSerializer.Serialize(request.Traceability);
        card.OperationsJson = JsonSerializer.Serialize(request.Operations);
        card.UpdatedUtc = DateTime.UtcNow;
        if (card.Id > 0)
        {
            var existingCard = await db.RouteCards.SingleOrDefaultAsync(value => value.Id == card.Id, cancellationToken);
            if (existingCard is null)
                throw new ArgumentException("The route card to update could not be found.", nameof(request));
            if (existingCard.CreatedByUserId != userId)
                throw new UnauthorizedAccessException("Only the user who created this route card can update it.");
            card.CreatedByUserId = existingCard.CreatedByUserId;
            card.CreatedUtc = existingCard.CreatedUtc;
            db.Entry(existingCard).CurrentValues.SetValues(card);
            card = existingCard;
            var existingApprovals = await db.RouteCardApprovals.Where(value => value.RouteCardId == card.Id).ToListAsync(cancellationToken);
            db.RouteCardApprovals.RemoveRange(existingApprovals);
        }
        else
        {
            card.CreatedByUserId = userId;
            db.RouteCards.Add(card);
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var approvalRequest in request.Approvals)
        {
            if (string.IsNullOrWhiteSpace(approvalRequest.OperationNo))
                throw new ArgumentException("Each approval must include an operation number.", nameof(request));
            var stamp = await db.UserStamps.Include(value => value.AppUser).SingleOrDefaultAsync(value =>
                value.Id == approvalRequest.StampId &&
                (value.AppUserId == userId || (serialNumber.HasValue && value.AppUser.SerialNumber == serialNumber.Value)), cancellationToken);
            if (stamp is null)
                throw new ArgumentException("The selected stamp does not belong to the signed-in user.", nameof(request));
            db.RouteCardApprovals.Add(new RouteCardApproval
            {
                RouteCardId = card.Id,
                AppUserId = stamp.AppUserId,
                UserStampId = stamp.Id,
                OperationNo = approvalRequest.OperationNo.Trim(),
                StampValue = stamp.StampValue,
                SignatureValue = stamp.SignatureValue,
                ApprovedAt = approvalRequest.ApprovedAt
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return card;
    }

    public Task<RouteCard?> GetAsync(long id, CancellationToken cancellationToken) =>
        db.RouteCards.AsNoTracking().SingleOrDefaultAsync(card => card.Id == id, cancellationToken);
}
