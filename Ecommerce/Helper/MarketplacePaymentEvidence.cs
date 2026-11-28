using MF.Models;
using System.Globalization;

namespace EcommerceWebApplication.Helper;

public static class MarketplacePaymentEvidence
{
    public static bool IsValidLookupId(string? paymentId) =>
        paymentId is { Length: > 0 and <= 100 } &&
        paymentId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    // A callback ID is only a lookup key. Trust the authenticated gateway response,
    // and require the successful transaction to be the one requested.
    public static long? VerifiedRecoveryInvoice(GenericResponse<GetPaymentStatusResponse>? response,
        string? lookupId, Guid reference, decimal amount)
    {
        if (!IsValidLookupId(lookupId) || response?.Data is not { InvoiceId: > 0 } data)
            return null;
        return string.Equals(VerifiedPaymentId(response, data.InvoiceId, reference, amount),
            lookupId, StringComparison.Ordinal) ? data.InvoiceId : null;
    }

    public static string? VerifiedPaymentId(GenericResponse<GetPaymentStatusResponse>? response, long invoiceId,
        Guid reference, decimal amount)
    {
        var data = response?.Data;
        if (invoiceId <= 0 || reference == Guid.Empty || amount <= 0 ||
            response?.IsSuccess != true || data == null || data.InvoiceId != invoiceId ||
            data.CustomerReference != reference.ToString("N") || data.InvoiceValue != amount ||
            !string.Equals(data.InvoiceStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
            data.InvoiceTransactions == null) return null;
        if (data.InvoiceTransactions.Any(t => t == null)) return null;
        var paid = data.InvoiceTransactions.Where(t => string.Equals(t.TransactionStatus,"Succss",StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.TransactionStatus,"Success",StringComparison.OrdinalIgnoreCase)).ToList();
        if (paid.Count != 1) return null;
        var transaction = paid[0];
        if (!IsKuwaitiCurrency(transaction.Currency) || !IsKuwaitiCurrency(transaction.PaidCurrency) ||
            !decimal.TryParse(transaction.TransationValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ||
            value != amount || !IsValidLookupId(transaction.PaymentId))
            return null;
        return transaction.PaymentId;
    }

    private static bool IsKuwaitiCurrency(string? currency) => currency is "KD" or "KWD";
}
