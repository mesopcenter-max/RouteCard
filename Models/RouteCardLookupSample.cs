namespace HALRouteCardApp.Models;

public sealed class RouteCardLookupSample
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string WorkOrderNo { get; set; } = string.Empty;
    public decimal? JcQty { get; set; }
    public string? Project { get; set; }
    public string? ProductType { get; set; }
    public string? PslNo { get; set; }
    public string PartNo { get; set; } = string.Empty;
    public string? PartName { get; set; }
    public string? HalDrawingNo { get; set; }
    public string? ReferenceDrawingNo { get; set; }
    public string? MaterialSpecification { get; set; }
    public string? MaterialDescription { get; set; }
    public string? MrNo { get; set; }
    public DateTime? MaterialDate { get; set; }
    public string? RawMaterialDia { get; set; }
    public string? RawMaterialLength { get; set; }
    public string? RawMaterialBatchNo { get; set; }
    public string? BsWeight { get; set; }
    public string? ForgingWeight { get; set; }
    public string? ForgingPlant { get; set; }
    public string? ForgingBatchNo { get; set; }
}
