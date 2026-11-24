namespace EcommerceWebApplication.Models.ViewModels
{
    public class SigleImageDto
    {
        public int ImageId { get; set; }
        public int ProductId { get; set; }
        public bool IsPrimary { get; set; }
        public string Base64 { get; set; }
    }
}
