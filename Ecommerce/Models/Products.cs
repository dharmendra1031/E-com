using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class Products
    {
        //[Key]   // ✅ Explicitly mark it as primary key

        //public int ProductId { get; set; }

        //public int CategoryId { get; set; }
        //public int? BrandId { get; set; }

        //[ForeignKey("BrandId")]
        //public virtual Brand Brand { get; set; }
        //public int SubCategoryId { get; set; }
        //[MaxLength(20)]
        //public string? Ds_Barcode { get; set; }

        //[MaxLength(20)]
        //public string? Ds_ProdID { get; set; }

        //public string Name { get; set; }
        //public string NameArb { get; set; } 

        //public string? Description { get; set; }
        //public string? DescriptionArb { get; set; }
        //[Column(TypeName = "decimal(18,3)")]
        //public decimal Price { get; set; }
        //[Column(TypeName = "decimal(18,3)")]
        //public decimal CoastPrice { get; set; }
        //public int Stock { get; set; }
        //public decimal? Weight { get; set; }

        //public DateTime CreatedAt { get; set; } = DateTime.Now;
        //public int CreatedBy { get; set; }


        //public string? SKUId { get; set; }     // Newly Added
        //public string? ProductsModel { get; set; }   // Newly Added
        //public string? Warranty { get; set; }        // Newly Added

        //public int? Units { get; set; }
        //public DateTime? UpdatedAt { get; set; }
        //public int? UpdatedBy { get; set; }
        //public bool IsActive { get; set; } = true;

        //// ✅ Navigation Properties

        //public virtual Category Category { get; set; }

        //public virtual SubCategory SubCategory { get; set; }
        //[NotMapped]
        //public virtual User CreatedUser { get; set; }
        //[NotMapped]
        //public virtual User UpdatedUser { get; set; }

        //public virtual ICollection<ProductImages> ProductImages { get; set; } = new List<ProductImages
        //


        [Key]
        public int ProductId { get; set; }

        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }

        public int? BrandId { get; set; }

        [ForeignKey("BrandId")]
        public virtual BrandModel Brand { get; set; }

        [MaxLength(20)]
        public string? Ds_Barcode { get; set; }

        [MaxLength(20)]
        public string? Ds_ProdID { get; set; }

        public string Name { get; set; }
        public string NameArb { get; set; }
        public string? Description { get; set; }
        public string? DescriptionArb { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal CoastPrice { get; set; }

        public int Stock { get; set; }
        public decimal? Weight { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CreatedBy { get; set; }

        public string? SKUId { get; set; }
        public string? ProductsModel { get; set; }
        public string? Warranty { get; set; }
        public int? Units { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual Category Category { get; set; }
        public virtual SubCategory SubCategory { get; set; }

        [NotMapped]
        public virtual User CreatedUser { get; set; }

        [NotMapped]
        public virtual User UpdatedUser { get; set; }

        public virtual ICollection<ProductImages> ProductImages { get; set; } = new List<ProductImages>();

        // 🔹 ProductReviews navigation property add karo
        public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    }

}
