namespace EcommerceWebApplication.Models
{
    public class ResetPasswordModel
    {
        public string? Email { get; set; }
        public string? mobile { get; set; }
        public string NewPassword { get; set; }
        public string deviceId { get; set; }
    }
}
