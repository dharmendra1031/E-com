using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Models.ViewModels
{
    [Keyless]
    public class ProductStockViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductNameArb { get; set; }
        public int TotalStock { get; set; }
        public int RemainingQty { get; set; }
        public int Totalsoldstock { get; set; }
        public string Status { get; set; }
        public string? Ds_Barcode { get; set; }
        public string? Ds_ProdID { get; set; }
    }
}
