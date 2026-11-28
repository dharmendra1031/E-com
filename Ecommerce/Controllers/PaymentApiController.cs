using EcommerceWebApplication.Helper;
using MF.Models;
using EcommerceWebApplication.Models;
using Microsoft.AspNetCore.Mvc;
using static EcommerceWebApplication.Controllers.EcommerceApiController;
namespace EcommerceWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentApiController : ControllerBase
    {
        public class SendPaymentFullRequest
        {
            public SendPaymentRequest Payment { get; set; } // MyFatoorah payment data
            public int UserId { get; set; }
            public BillingDetailsDto Billing { get; set; }
            public List<OrderItemDto> Items { get; set; }
            public decimal DiscountAmount { get; set; }
            public int DeliveryAddress { get; set; }
            public decimal VatPercent { get; set; }
            public decimal DeliveryCharge { get; set; }
            public decimal FinalTotal { get; set; }
        }
        [HttpPost("sendPayment")]
        public IActionResult SendPayment([FromBody] SendPaymentFullRequest request)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpPost("executePayment")]
        public IActionResult ExecutePayment([FromBody] ExecutePaymentRequest request)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpGet("payment-success")]
        public IActionResult paymentsuccess([FromQuery] string paymentId = null)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpGet("payment-failed")]
        public IActionResult PaymentFailed([FromQuery] string paymentId = null,string id=null)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpGet("verifyPayment/{paymentId}")]
        public IActionResult VerifyPayment(string paymentId)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpPost("getPaymentStatus")]
        public IActionResult GetPaymentStatus([FromBody] GetPaymentStatusRequest request)
        {
            return LegacyCheckoutUnavailable.Result();
        }

        [HttpGet("session")]
        public IActionResult Session()
        {
            return LegacyCheckoutUnavailable.Result();
        }
        public class OrderResponseDto
        {
            public string Message { get; set; }
            public int OrderId { get; set; }
            public decimal Subtotal { get; set; }
            public decimal Discount { get; set; }
            public decimal VatPercent { get; set; }
            public decimal VatAmount { get; set; }
            public decimal FinalTotal { get; set; }
        }

    }
}
