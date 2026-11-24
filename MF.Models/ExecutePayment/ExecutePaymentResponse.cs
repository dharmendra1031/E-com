using System;
using System.Collections.Generic;
using System.Text;

namespace MF.Models
{
  
    public class ExecutePaymentResponse
    {
        public long InvoiceId { get; set; }
        public bool IsDirectPayment { get; set; }
        public string PaymentURL { get; set; }
        public string CustomerReference { get; set; }
        public string UserDefinedField { get; set; }
        public string RecurringId { get; set; }

    }
}
