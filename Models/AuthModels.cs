namespace HALRouteCardApp.Models;

public sealed class AppUser
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public List<UserStamp> Stamps { get; set; } = [];
}

public sealed class UserStamp
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public string StampValue { get; set; } = string.Empty;
    public string SignatureValue { get; set; } = string.Empty;
    public AppUser AppUser { get; set; } = null!;
}

public sealed class RouteCardApproval
{
    public long Id { get; set; }
    public long RouteCardId { get; set; }
    public int AppUserId { get; set; }
    public int UserStampId { get; set; }
    public string OperationNo { get; set; } = string.Empty;
    public string StampValue { get; set; } = string.Empty;
    public string SignatureValue { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public RouteCard RouteCard { get; set; } = null!;
    public AppUser AppUser { get; set; } = null!;
    public UserStamp UserStamp { get; set; } = null!;
}
