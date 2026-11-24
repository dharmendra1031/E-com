using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class DirectPaymentRequest : DirectPaymentModel
    {
        [Required]
        public string PaymentURL { get; set; }
    }

}
