namespace HALRouteCardApp.Models;

public sealed class RouteCard
{
    public long Id { get; set; }
    public string? RouteCardNo { get; set; }
    public string? OrderNo { get; set; }
    public decimal? Qty { get; set; }
    public string? WorkOrderNo { get; set; }
    public decimal? JcQty { get; set; }
    public string PartNo { get; set; } = string.Empty;
    public string? PartName { get; set; }
    public string? PslNo { get; set; }
    public string? HalDrawingNo { get; set; }
    public string? IssueNo { get; set; }
    public string? PartsPerUnit { get; set; }
    public string? ReferenceDrawingNo { get; set; }
    public string? MaterialSpecification { get; set; }
    public string? MaterialIssue { get; set; }
    public string? MaterialDescription { get; set; }
    public string? MrNo { get; set; }
    public DateTime? MaterialDate { get; set; }
    public string? MaterialType { get; set; }
    public string? ReceivedCondition { get; set; }
    public string? RawMaterialDia { get; set; }
    public string? RawMaterialLength { get; set; }
    public string? RawMaterialBatchNo { get; set; }
    public string? AlternativeMaterialSize { get; set; }
    public string? RubberSpecification { get; set; }
    public DateTime? RubberMixDate { get; set; }
    public DateTime? RubberExpiryDate { get; set; }
    public decimal? RubberQty { get; set; }
    public string? RubberSupplier { get; set; }
    public string? TaPcRef { get; set; }
    public string? BsWeight { get; set; }
    public string? ForgingWeight { get; set; }
    public string? ForgingPlant { get; set; }
    public string? ForgingBatchNo { get; set; }
    public decimal? ForgingQty { get; set; }
    public string? RubberBatchNo { get; set; }
    public string? RubberLtrNo { get; set; }
    public string? UnFett { get; set; }
    public string? FettWeight { get; set; }
    public string? CastingBatchNo { get; set; }
    public decimal? CastingQty { get; set; }
    public string? PadWeight { get; set; }
    public string? PlatingBatchNo { get; set; }
    public string? MixBatchNo { get; set; }
    public decimal? PowderQty { get; set; }
    public string? TraceabilityJson { get; set; }
    public string? OperationsJson { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
