using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class GetPaymentStatusRequest
    {
        [Required]
        public string Key { get; set; }
        [Required]
        public string KeyType { get; set; } // InvoiceId  PaymentId
    }
}
