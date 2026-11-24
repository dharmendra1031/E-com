using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class UserAddressViewModel
    {
        [Key]
        public int AddressId { get; set; }
        public string Title { get; set; }
        public string HouseNo { get; set; }
        public string Street { get; set; }
        public string Floor { get; set; }
        public string Landmark { get; set; }
        public string Phone { get; set; }
        public bool? IsDefault { get; set; }

        public string AreaName { get; set; }
        public string BlockName { get; set; }
    }
}
