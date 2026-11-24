using System;
using System.Collections.Generic;
using System.Text;

namespace MF.Models
{
    public class CardInfoModel
    {
        public string Number { get; set; }
        public string ExpiryMonth { get; set; }
        public string ExpiryYear { get; set; }
        public string Brand { get; set; }
        public string Issuer { get; set; }
    }
}
