namespace EcommerceWebApplication.Models.ViewModels;

public class EditProductViewModel : ProductEditorFields
{
    public int ProductId { get; set; }
    public string CoastPriceStr { get; set; } = "";
    public string PriceStr { get; set; } = "";
    public bool IsActive { get; set; }
    public string? Features { get; set; }
    public List<ProductImageViewModel> ExistingImages { get; set; } = new();
}

public class ProductImageViewModel
{
    public int ImageId { get; set; }
    public string Base64Image { get; set; } = "";
}
