using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models
{
    public class SendNotificationModel
    {
        [Key]
        public int Id { get; set; }
        public string EmailSubject { get; set; }
        public string Body { get; set; }
        public string Customertype { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }

        // Extra: emails list
        [NotMapped]
        public List<Customers> UserEmails { get; set; }
    }
}
