namespace HALRouteCardApp.Models;

public sealed class RouteCardOperationAttachment
{
    public long Id { get; set; }
    public long RouteCardId { get; set; }
    public string OperationNo { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
