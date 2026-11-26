using MF.Models;
using System.Globalization;

namespace EcommerceWebApplication.Helper;

public static class MarketplacePaymentEvidence
{
    public static string? VerifiedPaymentId(GenericResponse<GetPaymentStatusResponse>? response, long invoiceId,
        Guid reference, decimal amount)
    {
        var data = response?.Data;
        if (response?.IsSuccess != true || data == null || data.InvoiceId != invoiceId ||
            data.CustomerReference != reference.ToString("N") || data.InvoiceValue != amount ||
            !string.Equals(data.InvoiceStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
            data.InvoiceTransactions == null) return null;
        var paid = data.InvoiceTransactions.Where(t => string.Equals(t.TransactionStatus,"Succss",StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.TransactionStatus,"Success",StringComparison.OrdinalIgnoreCase)).ToList();
        if (paid.Count != 1) return null;
        var transaction = paid[0];
        if (!IsKuwaitiCurrency(transaction.Currency) || !IsKuwaitiCurrency(transaction.PaidCurrency) ||
            !decimal.TryParse(transaction.TransationValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ||
            value != amount || string.IsNullOrWhiteSpace(transaction.PaymentId) || transaction.PaymentId.Length > 100)
            return null;
        return transaction.PaymentId;
    }

    private static bool IsKuwaitiCurrency(string? currency) => currency is "KD" or "KWD";
}
