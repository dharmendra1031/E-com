using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels;

public class ListingStockPage
{
    public int VendorId { get; set; }
    public int ListingId { get; set; }
    public string ProductName { get; set; } = "";
    public long OnHand { get; set; }
    public long Reserved { get; set; }
    public long Available { get; set; }
    public bool HasBalance { get; set; }
    public bool CanEdit { get; set; }
    public int Page { get; set; }
    public bool HasNext { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<StockMovementRow> Movements { get; set; } = new();
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public StockAdjustmentForm Adjustment { get; set; } = new();
}

public class StockMovementRow
{
    public long InventoryTransactionId { get; set; }
    public long OnHandDelta { get; set; }
    public long ReservedDelta { get; set; }
    public string SourceType { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class StockAdjustmentForm
{
    public Guid RequestId { get; set; }
    [Required] public string Direction { get; set; } = "Add";
    [Range(typeof(long), "1", "1000000000")] public long Quantity { get; set; } = 1;
    [Required, StringLength(200)] public string Reason { get; set; } = "";
}
