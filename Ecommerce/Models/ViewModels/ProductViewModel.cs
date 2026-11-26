namespace EcommerceWebApplication.Models.ViewModels;

public class ProductViewModel : ProductEditorFields
{
    public List<IFormFile> ProductImages { get; set; } = new();
}
