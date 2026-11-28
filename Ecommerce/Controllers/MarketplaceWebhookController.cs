using EcommerceWebApplication.Data;
using EcommerceWebApplication.Helper;
using MF.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace EcommerceWebApplication.Controllers;

[ApiController]
[Route("api/marketplace/myfatoorah/webhook")]
public sealed class MarketplaceWebhookController(EcommerceDbContext db, IPaymentService gateway,
    IConfiguration configuration, IWebHostEnvironment environment, ILogger<MarketplaceWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(65536)]
    [EnableRateLimiting("MarketplaceWebhook")]
    public async Task<IActionResult> Receive([FromBody] JsonElement body, CancellationToken ct)
    {
        if (!MarketplacePaymentReconciliation.SandboxEnabled(configuration, environment) ||
            !configuration.GetValue("Marketplace:SignedWebhookEnabled", false)) return NotFound();
        var secret = configuration["Marketplace:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503);
        if (!Request.IsHttps) return BadRequest();
        if (!Request.Headers.TryGetValue("myfatoorah-signature", out var signatures) || signatures.Count != 1)
            return Unauthorized();
        var payment = MarketplaceWebhookSignature.Verify(body, signatures[0], secret);
        if (payment == null) return Unauthorized();
        if (!payment.IsPaid) return Ok(); // Failed events never release inventory or overwrite success.
        try
        {
            return await MarketplacePaymentReconciliation.VerifyWebhookAsync(db, gateway, payment, ct)
                ? Ok() : StatusCode(503);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Do not log the signed payload, secret, card data or gateway response.
            logger.LogWarning("Marketplace webhook reconciliation failed for invoice {InvoiceId}.", payment.InvoiceId);
            return StatusCode(503);
        }
    }
}
