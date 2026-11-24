namespace EcommerceWebApplication.Models
{
    public class ReturnRequestDto
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public string RequestType { get; set; }
        public string? Reason { get; set; }
    }
}
