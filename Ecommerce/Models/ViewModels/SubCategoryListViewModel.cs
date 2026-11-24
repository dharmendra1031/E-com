namespace EcommerceWebApplication.Models.ViewModels
{
    public class SubCategoryListViewModel
    {
        public int SubCategoryId { get; set; }
        public int? ParentCategoryId { get; set; }
        public string ParentCategoryName { get; set; }
        public string SCName { get; set; }
        public string SCNameArb { get; set; }
        public string SCDescription { get; set; }
        public string SCDescriptionArb { get; set; }
        public string SubCategoryImageBase64 { get; set; } // ✅ Holds Base64 string
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

}
