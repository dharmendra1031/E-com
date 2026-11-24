using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class CheckoutviewModel
    {
        public int? UserId { get; set; }
        public int? SelectedAddressId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Usertype { get; set; }
        public string? Phone { get; set; }
        [Column(TypeName = "decimal(18,3)")]
        public decimal? DeliveryCharge { get; set; }
        public decimal? VatPercentage { get; set; }
        public decimal? CartTotal { get; set; }
        public List<UserAddress> AddressList { get; set; }
        public List<OrderSummaryItemViewModel> CartItems { get; set; }
    }
}
