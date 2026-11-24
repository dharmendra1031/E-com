using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class DirectPaymentModel
    {

        [Required]
        [StringLength(10)]
        public string PaymentType { get; set; } //card  token authorize
        public bool SaveToken { get; set; }
        public int? IntervalDays { get; set; } = 0;
        public CardModel Card { get; set; }
        public string Token { get; set; } = "";
        public bool Bypass3DS { get; set; } = true;
    }
}
