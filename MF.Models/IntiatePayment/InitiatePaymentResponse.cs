using System;
using System.Collections.Generic;
using System.Text;

namespace MF.Models
{
    public class InitiatePaymentResponse
    {
        public List<InitiatePaymentMethodsModel> PaymentMethods { get; set; }
    }
}
