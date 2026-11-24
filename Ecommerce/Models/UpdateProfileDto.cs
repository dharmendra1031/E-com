namespace EcommerceWebApplication.Models
{
    public class UpdateProfileDto
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }

        public string? FullNameArb { get; set; }
        public string? Phone { get; set; }
        public string? BirthDate { get; set; }
    }
}
