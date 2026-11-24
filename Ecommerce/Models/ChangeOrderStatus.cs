using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class ChangeOrderStatus
    {
        [Key]
        public int ChangeOrderStatusId { get; set; }

        public int OrderId { get; set; }

        public int PreviousStatus { get; set; }

        public int CurrentStatus { get; set; }

        public string ? Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public int CreatedBy { get; set; }
    }
}
