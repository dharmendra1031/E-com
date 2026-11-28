using EcommerceWebApplication.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EcommerceWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    [AutoValidateAntiforgeryToken]
    public class SmsController : ControllerBase
    {

        private readonly ISmsService _smsService;

        public SmsController(ISmsService smsService)
        {
            _smsService = smsService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendSms(string mobile, string message)
        {
            if (string.IsNullOrEmpty(mobile) || string.IsNullOrEmpty(message))
                return BadRequest("Mobile and message are required.");

            var result = await _smsService.SendSmsAsync(mobile, message);

            return Ok(result);
        }
        [HttpGet("Balance")]
        public async Task<IActionResult> GetBalance()
        {
            var result = await _smsService.BalanceAsync();

            return Ok(result);
        }
        [HttpPost("SendSms")]
        public async Task<IActionResult> Send(string mobile, string message)
        {
            if (string.IsNullOrEmpty(mobile) || string.IsNullOrEmpty(message))
                return BadRequest("Mobile and message are required.");

            var result = await _smsService.SendSms(mobile, message);

            return Ok(result);
        }
    }
}
