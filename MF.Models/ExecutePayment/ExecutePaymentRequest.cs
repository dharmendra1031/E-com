
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class ExecutePaymentRequest 
    {
        public ExecutePaymentRequest()
        {
            InvoiceItems = new List<InvoiceItemModel>();
        }
        [Required]
        public int? PaymentMethodId { get; set; } = 0;

        [StringLength(250)]
        public string CustomerName { get; set; }
        [StringLength(10)]
        public string DisplayCurrencyIso { get; set; }
        [StringLength(10)]
        public string MobileCountryCode { get; set; }
        [StringLength(11)]
        [RegularExpression(@"^(?:(\+)|(00)|(\\*)|())[0-9]{3,14}((\\#)|())$", ErrorMessage = "Invalid mobile number")]
        public string CustomerMobile { get; set; }
        [StringLength(254)]
        [EmailAddress]
        public string CustomerEmail { get; set; }
        [Required]
        public decimal InvoiceValue { get; set; }
        [Required]
        [StringLength(254)]
        [Url]
        public string CallBackUrl { get; set; }
        [Required]
        [StringLength(254)]
        [Url]
        public string ErrorUrl { get; set; }
        [StringLength(3)]
        public string Language { get; set; } = "en";

        [StringLength(50)]
        public string CustomerReference { get; set; }

        [StringLength(20)]
        public string CustomerCivilId { get; set; }
        [StringLength(500)]
        public string UserDefinedField { get; set; }

        public CustomerAddressModel CustomerAddress { get; set; } = null;

        public DateTime? ExpiryDate { get; set; }
        public int? SupplierCode { get; set; }
        public decimal? SupplierValue { get; set; }
        public List<InvoiceItemModel> InvoiceItems { get; set; } = null;
        public ShippingMethod? ShippingMethod { get; set; }
        public Consignee ShippingConsignee { get; set; }
        public List<InvoiceSuppliers> Suppliers { get; set; }
        public RecurringModel RecurringModel { get; set; } = null;
        public bool IsRecurring { get; set; }
    }

}
