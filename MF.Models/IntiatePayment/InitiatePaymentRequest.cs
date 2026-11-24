using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    
    public class InitiatePaymentRequest
    {
        [Required]
        public decimal InvoiceAmount { get; set; }
        [Required]
        [StringLength(3)]
        public string CurrencyIso { get; set; }
    }
   

}
