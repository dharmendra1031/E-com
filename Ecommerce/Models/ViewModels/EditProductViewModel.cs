using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class EditProductViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string NameArb { get; set; }
        [MaxLength(20)]
        public string? Ds_Barcode { get; set; }

        [MaxLength(20)]
        public string? Ds_ProdID { get; set; }
        public string? Description { get; set; }
        public string? DescriptionArb { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal Price { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal CoastPrice { get; set; }

       
        public string CoastPriceStr { get; set; }
       
        public string PriceStr { get; set; }

        public int Stock { get; set; }
        public decimal? Weight { get; set; }
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int? brandid { get; set; }
        public bool IsActive { get; set; }
        public string? SKUId { get; set; }     // Newly Added
        public string? ProductsModel { get; set; }   // Newly Added
        public string? Warranty { get; set; }        // Newly Added
        public string? Features { get; set; }        // Newly Added
        public int? Units { get; set; }

        // For showing existing images
        public List<SpecificationModel> Specifications { get; set; }
        public List<ProductImageViewModel> ExistingImages { get; set; }
    }

    public class ProductImageViewModel
    {
        public int ImageId { get; set; }
        public string Base64Image { get; set; }
    }

}
