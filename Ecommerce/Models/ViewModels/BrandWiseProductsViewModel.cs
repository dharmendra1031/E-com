namespace EcommerceWebApplication.Models.ViewModels
{
    public class BrandWiseProductsViewModel
    {
        public int BrandId { get; set; }
        public string BrandName { get; set; }
        public int Rating { get; set; }
        public string BrandImageBase64 { get; set; }
        public List<ProductListViewModel> Products { get; set; }
    }
}
