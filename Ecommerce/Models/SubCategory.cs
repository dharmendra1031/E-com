namespace EcommerceWebApplication.Models
{
    public class SubCategory
    {
        public int SubCategoryId { get; set; }
        public int ParentCategoryId { get; set; }
        public bool showinnav { get; set; } = true;
        public string SCName { get; set; }
        public string SCNameArb { get; set; }
        public string? SCDescription { get; set; }
        public string? SCDescriptionArb { get; set; }
        public byte[]? SubCategoryImage { get; set; }

        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        public virtual Category ParentCategory { get; set; }
    }

}
