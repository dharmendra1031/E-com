using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class InvoiceItemModel
    {
        [Required]
        [StringLength(250)]
        public string ItemName { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        [Range(0.0, 100.0)]
        public decimal? Weight { get; set; }

        [Range(0.0, 200.0)]
        public decimal? Width { get; set; }

        [Range(0.0, 200.0)]
        public decimal? Height { get; set; }

        [Range(0.0, 200.0)]
        public decimal? Depth { get; set; }



    }

}
