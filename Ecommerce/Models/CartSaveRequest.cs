using static EcommerceWebApplication.Controllers.EcommerceApiController;

namespace EcommerceWebApplication.Models
{

    public class CartSaveRequest
    {
        public int? UserId { get; set; } = 0;
        public string? DeviceId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

}
