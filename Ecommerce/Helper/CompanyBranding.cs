using EcommerceWebApplication.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceWebApplication.Helper;

public sealed record CompanyBranding(string Name, int CompanyId, bool HasLogo)
{
    public static async Task<CompanyBranding> ReadAsync(EcommerceDbContext db, bool arabic)
    {
        var company = await db.CompanyDetails.AsNoTracking().OrderBy(c => c.CompanyId)
            .Select(c => new { c.CompanyId, c.CompanyName, c.CompanyNameArabic, HasLogo = c.Logo != null })
            .FirstOrDefaultAsync();
        var name = arabic ? company?.CompanyNameArabic : company?.CompanyName;
        if (string.IsNullOrWhiteSpace(name))
            name = arabic ? company?.CompanyName : company?.CompanyNameArabic;
        return new CompanyBranding(string.IsNullOrWhiteSpace(name) ? (arabic ? "المتجر" : "Store") : name,
            company?.CompanyId ?? 0, company?.HasLogo == true);
    }
}
