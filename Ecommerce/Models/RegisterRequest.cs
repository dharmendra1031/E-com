namespace EcommerceWebApplication.Models
{
    public class RegisterRequest
    {
        public string FullName { get; set; }
        public string FullNameArb { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string? deviceId { get; set; }
        public string Password { get; set; }
        public string? Otp { get; set; }
        public DateTime BirthDate { get; set; }
        public int? ShareholderNo { get; set; }
    }
}
