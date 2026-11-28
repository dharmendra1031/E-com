using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels;

public class VendorListingsPage
{
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public bool CanEdit { get; set; }
    public bool IsAdmin { get; set; }
    public string? Search { get; set; }
    public string? Filter { get; set; }
    public int Page { get; set; }
    public bool HasNext { get; set; }
    public List<Row> Rows { get; set; } = new();

    public class Row
    {
        public int ListingId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string ProductNameArb { get; set; } = "";
        public string SellerSku { get; set; } = "";
        public string? CatalogSku { get; set; }
        public string? Barcode { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = "";
        public bool ProductActive { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}

public class VendorListingForm
{
    public int ListingId { get; set; }
    public int VendorId { get; set; }
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Required, StringLength(100)] public string SellerSku { get; set; } = "";
    [Range(typeof(decimal), "0", "999999999999999.999")]
    public decimal Price { get; set; }
    public string? Version { get; set; }
    public string? ProductName { get; set; }
}
