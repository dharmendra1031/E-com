using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using EcommerceWebApplication.Models;
using MF.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static EcommerceWebApplication.Controllers.EcommerceApiController;
namespace EcommerceWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentApiController : ControllerBase
    {
        private readonly EcommerceDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IPaymentService _paymentService;

        public PaymentApiController(IPaymentService paymentService, EcommerceDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
            _paymentService = paymentService;
        }
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
        // ----------------- SEND PAYMENT -----------------
        [HttpPost("sendPayment")]
        public async Task<IActionResult> SendPayment([FromBody] SendPaymentFullRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            if (string.IsNullOrEmpty(request.Payment.Language))
                request.Payment.Language = "EN";
            //  Create OrderItems & update stock
            foreach (var item in request.Items)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId);
                if (product == null)
                    throw new Exception($"Product not found: {item.ProductId}");
                if (product.Stock < item.Quantity)
                    throw new Exception($"Insufficient stock for product: {product.NameArb}");
            }
            // Callback URLs (for hosted MyFatoorah pages)
            //request.CallBackUrl ??= Url.Action("VerifyPayment", "PaymentApi", null, Request.Scheme);
            //request.ErrorUrl ??= Url.Action("PaymentFailed", "PaymentApi", null, Request.Scheme);
            request.Payment.CallBackUrl = "https://shopsabahiyacoop.com/payment-success";
            request.Payment.ErrorUrl = "https://shopsabahiyacoop.com/payment-failed";
            var response = await _paymentService.SendPayment(request.Payment);
            if (response.IsSuccess && response.Data != null)
            {
                //return Redirect(response.Data.InvoiceURL); // Non-direct payment
                // Non-direct payment → redirect URL in response
                //Place order saving      

                var transactionid = response.Data.InvoiceId.ToString();
                var result = await SaveOrderAsync(new PlaceOrderDto
                {
                    UserId = request.UserId,
                    BillingDetails = request.Billing,
                    Items = request.Items,
                    DiscountAmount = request.DiscountAmount,
                    VatPercent = request.VatPercent,
                    TotalAmount = request.FinalTotal,
                    DeliveryCharge=request.DeliveryCharge,
                    DeliveryAddress = request.DeliveryAddress
                }, transactionid);
                if (result is not OkObjectResult)
                    return result;
              
                //if (result is OkObjectResult okResult)
                //{
                //    var resultData = okResult.Value as dynamic; // or your actual type
                //    response.Data.UserDefinedField = resultData?.orderId;
                //}
                //response.Data.UserDefinedField = result.Value;
                return Ok(response);

            }
            return BadRequest(response);
        }

        // ----------------- EXECUTE PAYMENT -----------------
        [HttpPost("executePayment")]
        public async Task<IActionResult> ExecutePayment([FromBody] ExecutePaymentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrEmpty(request.Language))
                request.Language = "EN";

            if (!request.IsRecurring)
                request.RecurringModel = null;

            // Callback URLs
            //request.CallBackUrl ??= Url.Action("VerifyPayment", "PaymentApi", null, Request.Scheme);
            //request.ErrorUrl ??= Url.Action("PaymentFailed", "PaymentApi", null, Request.Scheme);

            request.CallBackUrl = "https://shopsabahiyacoop.com/payment-success";
            request.ErrorUrl = "https://shopsabahiyacoop.com/payment-failed";
            var response = await _paymentService.ExecutePayment(request);

            if (response.IsSuccess && response.Data != null)
            {
                if (!response.Data.IsDirectPayment)
                    return Redirect(response.Data.PaymentURL); // Non-direct payment return View("ExecutePaymentResult", response); // Direct payment }
                return Redirect(response.Data.PaymentURL);
            }

            return BadRequest(response);
        }

        // ----------------- VERIFY PAYMENT -----------------
        //[HttpGet("verifyPayment/{paymentId}")]
        //public async Task<IActionResult> VerifyPayment(string paymentId)
        //{
        //    if (string.IsNullOrEmpty(paymentId))
        //        return BadRequest(new { Message = "paymentId is required" });

        //    var request = new GetPaymentStatusRequest
        //    {
        //        KeyType = "PaymentId",
        //        Key = paymentId
        //    };

        //    var response = await _paymentService.GetPaymentStatus(request);

        //    if (response.IsSuccess)
        //        return Ok(response);

        //    return BadRequest(response);
        //}

        // ----------------- PAYMENT FAILED -----------------
        //[HttpGet("paymentFailed")]
        //public IActionResult PaymentFailed()
        //{
        //    return BadRequest(new { Message = "Payment Failed or Cancelled" });
        //}
        // ----------------- PAYMENT SUCCESS -----------------
        [HttpGet("payment-success")]
        public IActionResult paymentsuccess([FromQuery] string paymentId = null)
        {
            // Optional: you can redirect to your frontend success page or return JSON.
            if (!string.IsNullOrEmpty(paymentId))
            {
                // ✅ Redirect to frontend success page with paymentId
                return Redirect($"https://shopsabahiyacoop.com/payment-success?paymentId={paymentId}");
            }
            // Fallback JSON if no frontend
            return Ok(new
            {
                Success = true,
                Message = "Payment completed successfully!",
                PaymentId = paymentId
            });
        }

        // ----------------- PAYMENT FAILED -----------------
        [HttpGet("payment-failed")]
        public IActionResult PaymentFailed([FromQuery] string paymentId = null,string id=null)
        {
            // ✅ Redirect to frontend failure page
            if (!string.IsNullOrEmpty(paymentId))
            {
                 return Redirect($"https://shopsabahiyacoop.com/payment-failed?paymentId={paymentId}");
            }

            // Fallback JSON if no redirect
            return BadRequest(new
            {
                Success = false,
                Message = "Payment failed or was cancelled.",
                PaymentId = paymentId
            });
        }


        [HttpGet("verifyPayment/{paymentId}")]
        public async Task<IActionResult> VerifyPayment(string paymentId)
        {
            if (string.IsNullOrEmpty(paymentId))
                return BadRequest(new { Message = "PaymentId is required" });

            var request = new GetPaymentStatusRequest
            {
                KeyType = "PaymentId",
                Key = paymentId
            };

            var response = await _paymentService.GetPaymentStatus(request);

            if (response.IsSuccess && response.Data != null)
            {
                // ✅ Check MyFatoorah payment status
                if (response.Data.InvoiceStatus == "Paid")
                {


                    // ✅ Redirect to your frontend success page
                    return Redirect("https://shopsabahiyacoop.com/payment-success");
                }
                else
                {
                    // ❌ Redirect to your frontend failure page
                    return Redirect("https://shopsabahiyacoop.com/payment-failed");
                }
            }

            // ❌ If MyFatoorah API fails
            return BadRequest(new
            {
                Message = "Unable to verify payment.",
                Response = response
            });
        }

        // ----------------- PAYMENT FAILED -----------------
        //[HttpGet("paymentFailed")]
        //public IActionResult PaymentFailed()
        //{
        //    // ✅ Redirect to your frontend failure page instead of just BadRequest
        //    return Redirect("https://shopsabahiyacoop.com/payment-failed");
        //}

        [HttpPost("getPaymentStatus")]
        public async Task<IActionResult> GetPaymentStatus([FromBody] GetPaymentStatusRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var response = await _paymentService.GetPaymentStatus(request);

                if (response == null)
                    return NotFound(new { message = "Payment status not found." });

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching payment status.", error = ex.Message });
            }
        }

        [HttpGet("session")]
        public async Task<IActionResult> Session()
        {
            var session = await _paymentService.InitiateSessionAsync();
            dynamic json = JsonConvert.DeserializeObject(session);
            string sessionId = json.Data?.SessionId;
            return Ok(new { sessionId });
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

        [NonAction]
        private async Task<IActionResult> SaveOrderAsync(PlaceOrderDto orderRequest,string transactionid)
        {
            if (orderRequest == null || orderRequest.Items == null || !orderRequest.Items.Any())
                return BadRequest(new { message = "Invalid order request" });
             orderRequest.PaymentMethod = "Online";
            //  Validate stock using RemainingQty logic (before transaction)
            foreach (var item in orderRequest.Items)
            {
                int totalStock = await _context.ProductStocks
                    .Where(s => s.ProductId == item.ProductId)
                    .SumAsync(s => (int?)s.StockQty) ?? 0;

                int totalSold = await _context.OrderItems
                    .Where(o => o.ProductId == item.ProductId)
                    .SumAsync(o => (int?)o.Quantity) ?? 0;

                int remainingQty = totalStock - totalSold;

                if (remainingQty < item.Quantity)
                {
                    return BadRequest(new
                    {
                        message = $"Product ID {item.ProductId} has only {remainingQty} items left in stock."
                    });
                }
            }

            //  Start transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                //  Ensure valid SQL-safe datetime
                DateTime nowSqlSafe = DateTime.Now < new DateTime(1753, 1, 1)
                    ? new DateTime(1753, 1, 1)
                    : DateTime.Now;

                //   Get VAT % from latest company record
                var company = await _context.CompanyDetails
                    .OrderByDescending(c => c.CompanyId)
                    .FirstOrDefaultAsync();

                decimal vatPercent = company?.Vat ?? 0;
                decimal subtotal = orderRequest.Items.Sum(i => i.Price * i.Quantity);
                decimal discountAmount = orderRequest.DiscountAmount;
                decimal afterDiscount = Math.Max(subtotal - discountAmount, 0);
                decimal vatAmount = (afterDiscount * vatPercent) / 100;
                decimal finalTotal = afterDiscount + vatAmount;

                //  Create Order
                var order = new OrderModel
                {
                    CustomerId = orderRequest.UserId,
                    TotalAmount = finalTotal,
                    DiscountAmount = discountAmount,
                    VatPercent = vatPercent,
                    VatAmount = vatAmount,
                    Status = 1,                  // Pending
                    PaymentStatus = 3,           // Failed
                    PaymentType = orderRequest.PaymentMethod,
                    CouponCode = orderRequest.CouponCode,
                    CreatedAt = nowSqlSafe,
                    UpdatedAt = nowSqlSafe,
                    DeliveryAddress = orderRequest.DeliveryAddress,
                    OrderTransaction = transactionid,
                    DeliveryCharge = orderRequest.DeliveryCharge
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // To get OrderId

                //  Create OrderItems & update stock
                foreach (var item in orderRequest.Items)
                {
                    _context.OrderItems.Add(new OrderItemmodel
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    });

                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId);
                    if (product == null)
                        throw new Exception($"Product not found: {item.ProductId}");

                    if (product.Stock < item.Quantity)
                        throw new Exception($"Insufficient stock for product: {product.NameArb}");

                    product.Stock -= item.Quantity;
                    _context.Products.Update(product);
                }

                //   Add Payment record
                _context.Payments.Add(new Payments
                {
                    OrderId = order.OrderId,
                    AmountPaid = finalTotal,
                    PaymentMethod = orderRequest.PaymentMethod,
                    PaymentStatus = 3, // Unpaid
                    TransactionId = transactionid,
                    PaidAt = nowSqlSafe
                });

                //  Add Coupon usage (if applicable)
                if (orderRequest.CouponID > 0)
                {
                    _context.CouponUsage.Add(new CouponUsage
                    {
                        CouponId = orderRequest.CouponID,
                        UserId = order.CustomerId,
                        UsedAt = nowSqlSafe
                    });
                }

                ////  Clear user's cart (optional but recommended)
                //var cartItems = await _context.CartItems
                //    .Where(c => c.UserId == orderRequest.UserId)
                //    .ToListAsync();

                //if (cartItems.Any())
                //    _context.CartItems.RemoveRange(cartItems);

                // Save all pending changes
                await _context.SaveChangesAsync();

                //  Commit the transaction
                await transaction.CommitAsync();

                //  Return success response
                return Ok(new
                {
                    message = "Order placed successfully!",
                    orderId = order.OrderId,
                    subtotal,
                    discount = discountAmount,
                    vatPercent,
                    vatAmount,
                    finalTotal
                });
            }
            catch (Exception ex)
            {
                // Rollback if any error occurs
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    message = "Failed to place order.",
                    error = ex.Message
                });
            }
        }
    }
}
