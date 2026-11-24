using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class CardModel
    {
        [StringLength(80)]
        public string Number { get; set; }

        [StringLength(2)]
        public string ExpiryMonth { get; set; }

        [StringLength(2)]
        public string ExpiryYear { get; set; }

        [StringLength(4)]
        public string SecurityCode { get; set; }
        [StringLength(50)]
        public string HolderName { get; set; }
    }

}
