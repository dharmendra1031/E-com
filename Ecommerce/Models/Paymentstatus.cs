using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class Paymentstatus
    {
        [Key]
        public int PaymentstatusId { get; set; }        
        public string ?  PaymentstatusName { get; set; }     
        public string ? PaymentStatusNameArb { get; set; }     
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
    }
}
