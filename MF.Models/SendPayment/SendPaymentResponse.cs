using System;
using System.Collections.Generic;
using System.Text;

namespace MF.Models
{

    public class SendPaymentResponse
    {
        public int InvoiceId { get; set; }
        public string InvoiceURL { get; set; }
        public string CustomerReference { get; set; }
        public string UserDefinedField { get; set; }
    }

   
}
