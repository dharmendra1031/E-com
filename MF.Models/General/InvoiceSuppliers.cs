using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class InvoiceSuppliers
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int SupplierCode { get; set; }
        public decimal? ProposedShare { get; set; }
        [Required]
        public decimal InvoiceShare { get; set; }
    }
}
