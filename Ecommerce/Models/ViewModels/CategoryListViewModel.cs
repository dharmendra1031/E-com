namespace EcommerceWebApplication.Models.ViewModels
{
    public class CategoryListViewModel
    {
        public int CategoryId { get; set; }
        public string CName { get; set; }
        public string CNameArb { get; set; }
        public string? CDescription { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // ✅ Base64 image to show in View
        public string CategoryImageBase64 { get; set; }
        public string CategoryIconBase64 { get; set; }
    }

}
