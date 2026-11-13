namespace HALRouteCardApp.Models;

public sealed class RouteCardLookupRequest
{
    public string? OrderNo { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? PartNo { get; set; }
}

public sealed class RouteCardLookupResult
{
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? PslNo { get; set; }
    public string? OrderNo { get; set; }
    public decimal? Qty { get; set; }
    public string? WorkOrderNo { get; set; }
    public decimal? JcQty { get; set; }
    public string? SourceTable { get; set; }
    public string? OperationsMessage { get; set; }
    public List<string> SourceDatabases { get; set; } = [];
    public List<string> SourceTables { get; set; } = [];
    public Dictionary<string, string?> MatchedFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<RouteCardLookupRecord> Records { get; set; } = [];
    public List<RouteCardOperationLookup> Operations { get; set; } = [];
}

public sealed class RouteCardLookupRecord
{
    public string Database { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RouteCardOperationLookup
{
    public string? PartNo { get; set; }
    public string? OpNo { get; set; }
    public string? Uid { get; set; }
    public string? OperationName { get; set; }
    public string Revision { get; set; } = "A";
    public decimal? OpTimePerItemInSeconds { get; set; }
    public string? NoteText { get; set; }
    public string? WorkcenterDepartment { get; set; }
}

public sealed class RouteCardSaveRequest
{
    public RouteCard Card { get; set; } = new();
    public object[] Traceability { get; set; } = [];
    public object[] Operations { get; set; } = [];
    public List<RouteCardOperationApprovalRequest> Approvals { get; set; } = [];
}

public sealed class RouteCardOperationApprovalRequest
{
    public string? OperationNo { get; set; }
    public int StampId { get; set; }
    public DateTime ApprovedAt { get; set; }
}
