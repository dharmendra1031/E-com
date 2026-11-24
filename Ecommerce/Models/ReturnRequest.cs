using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class ReturnRequest
    {
        [Key]
        public int RequestId { get; set; }

        // Foreign Keys
        public int OrderId { get; set; }
        public int UserId { get; set; }

        public string RequestType { get; set; }   // e.g., Refund, Replacement
        public string? Reason { get; set; }        // Optional reason
        public string? Status { get; set; }        // Pending / Approved / Rejected
        public DateTime RequestedAt { get; set; } // Defaults to current date
        public DateTime? ProcessedAt { get; set; } // Nullable

        // ✅ Navigation properties
        public virtual OrderModel Order { get; set; }
        public virtual User User { get; set; }
    }

}
