namespace EcommerceWebApplication.Models
{
    public class OtpRequest
    {
        public string? email { get; set; }
        public string? mobile { get; set; }
        public string? fp { get; set; } = string.Empty;
    }
}
