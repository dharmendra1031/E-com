using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models.ViewModels;

public class ProductEditorFields
{
    public int CategoryId { get; set; }
    public int SubCategoryId { get; set; }
    public int? brandid { get; set; }
    public string Name { get; set; } = "";
    public string NameArb { get; set; } = "";
    public string? Description { get; set; }
    public string? DescriptionArb { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal Price { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal CoastPrice { get; set; }
    public int Stock { get; set; }
    public decimal? Weight { get; set; }
    [MaxLength(20)] public string? Ds_Barcode { get; set; }
    [MaxLength(20)] public string? Ds_ProdID { get; set; }
    public string? SKUId { get; set; }
    public string? ProductsModel { get; set; }
    public string? Warranty { get; set; }
    public int? Units { get; set; }
    public List<SpecificationModel> Specifications { get; set; } = new();
}
