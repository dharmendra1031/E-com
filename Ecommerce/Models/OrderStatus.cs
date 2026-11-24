using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class OrderStatus
    {
        [Key]
        public int OrderstatusId { get; set; }          
        public string OrderstatusName { get; set; }     
        public string OrderStatusNameArb { get; set; }     
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }

       
        public virtual ICollection<OrderModel> Orders { get; set; }
    }

}
