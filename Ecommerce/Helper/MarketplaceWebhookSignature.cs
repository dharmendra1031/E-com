using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcommerceWebApplication.Helper;

public static class MarketplaceWebhookSignature
{
    public sealed record PaymentEvent(long InvoiceId, string PaymentId, bool IsPaid);

    // MyFatoorah webhook V2 PAYMENT_STATUS_CHANGED canonical field order.
    public static PaymentEvent? Verify(JsonElement body, string? signature, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || signature is not { Length: 44 }) return null;
        Span<byte> supplied = stackalloc byte[32];
        if (!Convert.TryFromBase64String(signature, supplied, out var length) || length != 32) return null;
        if (!Field(body, "Event", out var evt) || !Field(evt, "Code", out var code) ||
            code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var n) || n != 1 ||
            !Field(evt, "Name", out var name) || name.ValueKind != JsonValueKind.String ||
            name.GetString() != "PAYMENT_STATUS_CHANGED" ||
            !Field(body, "Data", out var data) || !Field(data, "Invoice", out var invoice) ||
            !Field(data, "Transaction", out var transaction) ||
            !StringField(invoice, "Id", out var invoiceId) ||
            !long.TryParse(invoiceId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0 ||
            !StringField(invoice, "Status", out var invoiceStatus) ||
            !StringField(transaction, "Status", out var transactionStatus) ||
            !StringField(transaction, "PaymentId", out var paymentId) ||
            !StringField(invoice, "ExternalIdentifier", out var external, allowNull: true) ||
            !MarketplacePaymentEvidence.IsValidLookupId(paymentId)) return null;

        var canonical = $"Invoice.Id={invoiceId},Invoice.Status={invoiceStatus},Transaction.Status={transactionStatus},Transaction.PaymentId={paymentId},Invoice.ExternalIdentifier={external}";
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical));
        return CryptographicOperations.FixedTimeEquals(expected, supplied)
            ? new PaymentEvent(parsed, paymentId, invoiceStatus == "PAID" && transactionStatus == "SUCCESS")
            : null;
    }

    private static bool Field(JsonElement obj, string name, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object) return false;
        var found = false;
        foreach (var property in obj.EnumerateObject())
        {
            if (!property.NameEquals(name)) continue;
            if (found) return false;
            found = true;
            value = property.Value;
        }
        return found;
    }

    private static bool StringField(JsonElement obj, string name, out string value, bool allowNull = false)
    {
        value = "";
        if (!Field(obj, name, out var field)) return false;
        if (allowNull && field.ValueKind == JsonValueKind.Null) return true;
        if (field.ValueKind != JsonValueKind.String) return false;
        value = field.GetString()!;
        // Delimiter injection and oversized fields are outside our supported marketplace format.
        return value.Length <= 200 && !value.Any(c => c is ',' or '=' || char.IsControl(c));
    }
}
