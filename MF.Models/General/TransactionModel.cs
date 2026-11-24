using System;

namespace MF.Models
{
    public class TransactionModel
    {
        public DateTime TransactionDate { get; set; }
        public string PaymentGateway { get; set; }
        public string ReferenceId { get; set; }
        public string TrackId { get; set; }
        public string TransactionId { get; set; }
        public string PaymentId { get; set; }
        public string AuthorizationId { get; set; }
        public string TransactionStatus { get; set; }
        public string TransationValue { get; set; }
        public string CustomerServiceCharge { get; set; }
        public string DueValue { get; set; }// Transation Value + customer service charges (Local Currency)

        public string PaidCurrency { get; set; }
        public string PaidCurrencyValue { get; set; }


        public string Currency { get; set; }
        public string Error { get; set; }
        public string CardNumber { get; set; }
    }

}