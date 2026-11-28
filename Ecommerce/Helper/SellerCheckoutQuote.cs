using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace EcommerceWebApplication.Helper;

public record SellerQuoteLine(int ListingId, int Quantity, decimal Price);

public record SellerCheckoutQuote(Guid RequestId, int CustomerId, DateTime ExpiresAtUtc,
    decimal DeliveryCharge, decimal VatPercent, SellerQuoteLine[] Lines)
{
    public const decimal MaxMoney = 999999999999999.999m;
    public decimal Subtotal => Lines.Sum(l => l.Price * l.Quantity);
    public decimal VatAmount => Math.Round(Subtotal * VatPercent / 100m, 3, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + VatAmount + DeliveryCharge;

    public bool IsValid() => RequestId != Guid.Empty && CustomerId > 0 && Lines is { Length: > 0 and <= 100 }
        && Lines.All(l => l != null && l.ListingId > 0 && l.Quantity is > 0 and <= 1000000 &&
            l.Price >= 0 && l.Price <= MaxMoney && decimal.Round(l.Price, 3) == l.Price)
        && Lines.Select(l => l.ListingId).Distinct().Count() == Lines.Length
        && DeliveryCharge >= 0 && DeliveryCharge <= MaxMoney && decimal.Round(DeliveryCharge, 3) == DeliveryCharge
        && VatPercent is >= 0 and <= 100 && Total > 0 && Total <= MaxMoney;

    public string Protect(IDataProtector protector) => protector.Protect(JsonSerializer.Serialize(this));

    public static SellerCheckoutQuote? Read(IDataProtector protector, string? token, int customerId)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 30000) return null;
        try
        {
            var quote = JsonSerializer.Deserialize<SellerCheckoutQuote>(protector.Unprotect(token));
            return quote != null && quote.CustomerId == customerId && quote.IsValid() ? quote : null;
        }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
    }

    public bool MatchesLines(IEnumerable<SellerQuoteLine> lines) =>
        Lines.OrderBy(l => l.ListingId).SequenceEqual(lines.OrderBy(l => l.ListingId));
}
