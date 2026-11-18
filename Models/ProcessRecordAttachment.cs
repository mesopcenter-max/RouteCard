namespace HALRouteCardApp.Models;

public sealed class ProcessRecordAttachment
{
    public long Id { get; set; }
    public long RouteCardId { get; set; }
    public string OperationNo { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public int AttachedByUserId { get; set; }
    public string AttachedByName { get; set; } = string.Empty;
    public DateTime AttachedAtUtc { get; set; }
    public RouteCard RouteCard { get; set; } = null!;
    public AppUser AttachedByUser { get; set; } = null!;
}
