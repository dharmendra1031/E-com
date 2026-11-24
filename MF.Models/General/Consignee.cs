using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class Consignee
    {
        [Required]
        [StringLength(250)]
        public string PersonName { get; set; }

        [Required]
        [RegularExpression(@"[0-9]*")]
        [StringLength(10)]
        public string Mobile { get; set; }

        [StringLength(254)]
        [EmailAddress]
        public string EmailAddress { get; set; }

        [DataType(DataType.MultilineText)]
        [Required]
        [StringLength(250)]
        public string LineAddress { get; set; }

        [Required]
        public string CityName { get; set; }

        [StringLength(10)]
        public string PostalCode { get; set; }

        [Required]
        public string CountryCode { get; set; }
    }

}
