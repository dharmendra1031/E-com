using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class CustomerAddressModel
    {
        [StringLength(10)]
        public string Block { get; set; }

        [StringLength(50)]
        public string Street { get; set; }

        [StringLength(50)]
        public string HouseBuildingNo { get; set; }

        [StringLength(250)]
        public string AddressInstructions { get; set; }
    }

}
