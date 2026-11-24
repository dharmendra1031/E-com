namespace MF.Models
{
    public class SupplierModel
    {
        public int SupplierCode { get; set; }
        public string SupplierName { get; set; }
        public decimal InvoiceShare { get; set; }
        public decimal? ProposedShare { get; set; }
        public decimal? DepositShare { get; set; }

    }
}