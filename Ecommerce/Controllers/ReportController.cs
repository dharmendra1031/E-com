using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using EcommerceWebApplication.Data;
using EcommerceWebApplication.Models;
using EcommerceWebApplication.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Reporting.NETCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace EcommerceWebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly EcommerceDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ReportController(EcommerceDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }


        #region Daily sale report

        public ActionResult Index()

        {

            var culture = System.Globalization.CultureInfo.CurrentCulture;

            bool isArabic = culture.TextInfo.IsRightToLeft;

            ViewBag.StatusList = _context.OrderStatus

                .Select(s => new SelectListItem

                {

                    Value = s.OrderstatusId.ToString(),

                    Text = isArabic ? s.OrderStatusNameArb : s.OrderstatusName

                }).ToList();

            ViewBag.Selectedpaymentstatus = _context.PaymentStatus

                .Select(s => new SelectListItem

                {

                    Value = s.PaymentstatusId.ToString(),

                    Text = isArabic ? s.PaymentStatusNameArb : s.PaymentstatusName

                }).ToList();

            return View();

        }

        public async Task<IActionResult> ReportPrint(ReportViewModel rptParameters, string format = "PDF")
        {
            try
            {

                DateTime? dateFrom = null, dateTo = null;

                if (!string.IsNullOrWhiteSpace(rptParameters.DateFrom))
                {
                    dateFrom = DateTime.ParseExact(
                        rptParameters.DateFrom,
                        "dd-MM-yyyy",
                        CultureInfo.InvariantCulture
                    );
                }

                if (!string.IsNullOrWhiteSpace(rptParameters.DateTo))
                {
                    dateTo = DateTime.ParseExact(
                        rptParameters.DateTo,
                        "dd-MM-yyyy",
                        CultureInfo.InvariantCulture
                    );
                }


                var p1 = new SqlParameter("@OrderStatus", (object?)rptParameters.OrderStatus ?? DBNull.Value);
                var p2 = new SqlParameter("@PaymentStatus", (object?)rptParameters.PaymentStatus ?? DBNull.Value);
                var p3 = new SqlParameter("@DateFrom", (object?)dateFrom ?? DBNull.Value) { SqlDbType = SqlDbType.DateTime };
                var p4 = new SqlParameter("@DateTo", (object?)dateTo ?? DBNull.Value) { SqlDbType = SqlDbType.DateTime };
                var p5 = new SqlParameter("@FromOrderNo", (object?)rptParameters.FromOrderNo ?? DBNull.Value);
                var p6 = new SqlParameter("@ToOrderNo", (object?)rptParameters.ToOrderNo ?? DBNull.Value);

                var rows = await _context.DayWiseSaleReportModel
                    .FromSqlRaw(@"EXEC dbo.sp_DayWiseSaleReport 
                          @OrderStatus, @PaymentStatus, @DateFrom, @DateTo, @FromOrderNo, @ToOrderNo",
                                  p1, p2, p3, p4, p5, p6)
                    .ToListAsync();

                if (rows == null || !rows.Any())
                    return Content("No data available for the report.");
                if (format == "PDF")
                {

                    var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                    bool isArabic = culture == "ar";


                    var rdlcFile = isArabic ? "DayWiseSales_Ar.rdlc" : "DayWiseSales.rdlc";
                    var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                    using var defStream = System.IO.File.OpenRead(rdlcPath);
                    var report = new LocalReport { EnableExternalImages = true };
                    report.LoadReportDefinition(defStream);

                    report.DataSources.Add(new ReportDataSource("DataSet1", rows));


                    var company = await _context.CompanyDetails.FirstOrDefaultAsync();
                    string companyName = isArabic ? company?.CompanyNameArabic ?? "" : company?.CompanyName ?? "";
                    string companyAddress = isArabic ? company?.AddressArabic ?? "" : company?.Address ?? "";
                    string phone = company?.Phone ?? "";

                    string imagePath = "";
                    if (company?.Logo != null && company.Logo.Length > 0)
                    {
                        var folder = Path.Combine(_env.WebRootPath, "Temp");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var fileName2 = "logo.png";
                        imagePath = Path.Combine(folder, fileName2);
                        System.IO.File.WriteAllBytes(imagePath, company.Logo);

                        imagePath = new Uri(imagePath).AbsoluteUri;
                    }


                    var pFrom = dateFrom?.ToString("dd-MM-yyyy") ?? "";
                    var pTo = dateTo?.ToString("dd-MM-yyyy") ?? "";
                    var pGenAt = DateTime.Now.ToString("dd-MM-yyyy HH:mm");

                    var firstRow = rows.FirstOrDefault();

                    string? orderStatusText = null;
                    string? paymentStatusText = null;

                    if (!string.IsNullOrEmpty(rptParameters.OrderStatus))
                    {
                        orderStatusText = isArabic
                            ? firstRow?.OrderStatusNameArb
                            : firstRow?.OrderstatusName;
                    }

                    if (!string.IsNullOrEmpty(rptParameters.PaymentStatus))
                    {
                        paymentStatusText = isArabic
                            ? firstRow?.PaymentStatusNameArb
                            : firstRow?.PaymentstatusName;
                    }

                    report.SetParameters(new[]
                    {
    new ReportParameter("CompanyName", companyName),
    new ReportParameter("Address", companyAddress),
    new ReportParameter("Phone", phone),
    new ReportParameter("Logo", imagePath),
    new ReportParameter("UserName", User?.Identity?.Name ?? "Admin"),
    new ReportParameter("pFrom", pFrom),
    new ReportParameter("pTo", pTo),
    new ReportParameter("pGeneratedAt", pGenAt),


    new ReportParameter("pOrderStatus", orderStatusText ?? ""),
    new ReportParameter("pPaymentStatus", paymentStatusText ?? ""),
    new ReportParameter("pFromOrderNo", rptParameters.FromOrderNo?.ToString() ?? ""),
    new ReportParameter("pToOrderNo", rptParameters.ToOrderNo?.ToString() ?? "")
});



                    //var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    //{
                    //    "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                    //    "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                    //    _ => ("PDF", "application/pdf", "pdf")
                    //};

                    //byte[] bytes = report.Render(renderFormat);
                    //var fileName = $"DayWiseSale_{DateTime.Now:yyyyMMddHHmm}.{ext}";

                    //return File(bytes, mime, fileName);


                    var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    {
                        "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                        "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                        _ => ("PDF", "application/pdf", "pdf")
                    };

                    byte[] bytes = report.Render(renderFormat);


                    var fileName = $"DailySaleReport{DateTime.Now:yyyyMMddHHmm}.{ext}";


                    Response.Headers.Add("Content-Disposition", "inline; filename=" + fileName);

                    return File(bytes, mime);
                }
                else
                {
                    using (XLWorkbook wb = new XLWorkbook())
                    {
                        // 🔹 Culture check
                        var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                        bool isArabic = culture == "ar";

                        var worksheet = wb.Worksheets.Add(isArabic ? "المبيعات اليومية" : "DaywiseSale");

                        // 🔹 Header row
                        worksheet.Cell(1, 1).Value = isArabic ? "معرف الطلب" : "Order Id";
                        worksheet.Cell(1, 2).Value = isArabic ? "الحالة" : "Status";
                        worksheet.Cell(1, 3).Value = isArabic ? "طريقة الدفع" : "Pay Type";
                        worksheet.Cell(1, 4).Value = isArabic ? "معرف المنتج" : "Product Id";
                        worksheet.Cell(1, 5).Value = isArabic ? "الاسم" : "Name";
                        worksheet.Cell(1, 6).Value = isArabic ? "سعر البيع" : "Sale Price";
                        worksheet.Cell(1, 7).Value = isArabic ? "سعر التكلفة" : "Cost Price";
                        worksheet.Cell(1, 8).Value = isArabic ? "الكمية" : "Quantity";
                        worksheet.Cell(1, 9).Value = isArabic ? "الإجمالي" : "Total";

                        int row = 2;
                        foreach (var sale in rows)
                        {
                            worksheet.Cell(row, 1).Value = sale.OrderId;
                            worksheet.Cell(row, 2).Value = isArabic ? sale.OrderStatusNameArb : sale.OrderstatusName;
                            worksheet.Cell(row, 3).Value = sale.PaymentType;
                            worksheet.Cell(row, 4).Value = sale.ProductId;
                            worksheet.Cell(row, 5).Value = isArabic ? sale.NameArb : sale.Name;
                            worksheet.Cell(row, 6).Value = sale.Price;
                            worksheet.Cell(row, 7).Value = sale.CostPrice;
                            worksheet.Cell(row, 8).Value = sale.Quantity;
                            worksheet.Cell(row, 9).Value = sale.TotalAmount;
                            row++;
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            wb.SaveAs(ms);

                            var fileName = isArabic
                                ? $"المبيعات_اليومية_{DateTime.Now:yyyy-MM-dd}.xlsx"
                                : $"DaywiseSale_{DateTime.Now:yyyy-MM-dd}.xlsx";

                            return File(ms.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                        fileName);
                        }
                    }


                }
                return Ok();
            }
            catch (Exception ex)
            {
                return Content("An error occurred while generating the report: " + ex.Message);
            }
        }




        public class DayWiseSaleReportModel
        {

            public int? OrderId { get; set; }
            public DateTime? CreatedAt { get; set; }
            public decimal? TotalAmount { get; set; }
            public decimal? DiscountAmount { get; set; }


            public string? OrderstatusName { get; set; }
            public string? OrderStatusNameArb { get; set; }
            public string? PaymentStatusNameArb { get; set; }
            public string? PaymentstatusName { get; set; }


            public int? UserId { get; set; }
            public string? PaymentType { get; set; }
            public string? FullName { get; set; }
            public string? FullNameArb { get; set; }


            public int? OrderItemId { get; set; }
            public int? ProductId { get; set; }
            public int? Quantity { get; set; }


            public string? Name { get; set; }
            public string? NameArb { get; set; }
            public decimal? Price { get; set; }
            public decimal? CostPrice { get; set; }


            public int? CategoryId { get; set; }
            public string? CName { get; set; }
            public string? CNameArb { get; set; }
        }

        #endregion

        #region Category Wise Sales 
        public ActionResult CategoryWiseSalereport()

        {

            var culture = System.Globalization.CultureInfo.CurrentCulture;

            bool isArabic = culture.TextInfo.IsRightToLeft;




            ViewBag.CategoryList = _context.Categories

                .Select(c => new SelectListItem

                {

                    Value = c.CategoryId.ToString(),

                    Text = isArabic ? c.CNameArb : c.CName

                }).ToList();

            return View();

        }
        public async Task<IActionResult> CategoryReportPrint(int? categoryId, string format = "PDF")
        {
            try
            {

                var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                bool isArabic = culture == "ar";


                var rows = await _context.Set<CategoryReportModel>()
     .FromSqlRaw("EXEC dbo.sp_CategoryReport {0}", categoryId.Value != 0 ? categoryId.Value : (object)DBNull.Value)
     .ToListAsync();

              

                if (rows == null || !rows.Any())
                    return Content("No data available for the report.");

                if (format == "PDF")
                {
                    if (isArabic)
                    {
                        foreach (var r in rows)
                        {
                            r.CName = r.CNameArb;
                            r.Name = r.NameArb;
                            r.FullName = r.FullNameArb;
                            r.OrderStatusName = r.OrderStatusNameArb;
                        }
                    }


                    var rdlcFile = isArabic ? "CategoryWiseReport_Ar.rdlc" : "CategoryWiseReport.rdlc";
                    var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                    using var defStream = System.IO.File.OpenRead(rdlcPath);
                    var report = new LocalReport
                    {
                        EnableExternalImages = true
                    };
                    report.LoadReportDefinition(defStream);


                    report.DataSources.Add(new ReportDataSource("DataSet2", rows));


                    var company = await _context.CompanyDetails.FirstOrDefaultAsync();
                    if (company == null)
                        return Content("Company details not found.");

                    string companyName = isArabic ? company.CompanyNameArabic : company.CompanyName;
                    string companyAddress = isArabic ? company.AddressArabic : company.Address;
                    string phone = company.Phone;

                    string imagePath = "";
                    if (company.Logo != null && company.Logo.Length > 0)
                    {
                        var folder = Path.Combine(_env.WebRootPath, "Temp");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var fileName2 = "logo.png";
                        imagePath = Path.Combine(folder, fileName2);
                        System.IO.File.WriteAllBytes(imagePath, company.Logo);

                        imagePath = new Uri(imagePath).AbsoluteUri;
                    }


                    string? pCat = categoryId.HasValue
         ? rows.FirstOrDefault(r => r.CategoryId == categoryId)?.CName
         : null;

                    var pGenAt = DateTime.Now.ToString("dd-MMM-yyyy HH:mm");

                    report.SetParameters(new[]
                    {
            new ReportParameter("pCategoryName", pCat),
            new ReportParameter("pGeneratedAt", pGenAt),
            new ReportParameter("CompanyName", companyName ?? ""),
            new ReportParameter("Address", companyAddress ?? ""),
            new ReportParameter("Phone", phone ?? ""),
            new ReportParameter("Logo", imagePath ?? ""),
            new ReportParameter("UserName", "Admin")
        });


                    //var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    //{
                    //    "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                    //    "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                    //    _ => ("PDF", "application/pdf", "pdf")
                    //};

                    //byte[] bytes = report.Render(renderFormat);
                    //var fileName = $"CategoryReport_{DateTime.Now:yyyyMMddHHmm}.{ext}";

                    //return File(bytes, mime, fileName);

                    var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    {
                        "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                        "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                        _ => ("PDF", "application/pdf", "pdf")
                    };

                    byte[] bytes = report.Render(renderFormat);


                    var fileName = $"CategorySaleReport_{DateTime.Now:yyyyMMddHHmm}.{ext}";


                    Response.Headers.Add("Content-Disposition", "inline; filename=" + fileName);

                    return File(bytes, mime);
                }
                else
                {
                    using (XLWorkbook wb = new XLWorkbook())
                    {
                        var worksheet = wb.Worksheets.Add(isArabic ? "المبيعات اليومية" : "DaywiseSale");

                        // 🔹 Header row (Culture wise)
                        worksheet.Cell(1, 1).Value = isArabic ? "معرف الفئة" : "Category ID";
                        worksheet.Cell(1, 2).Value = isArabic ? "اسم الفئة" : "Category Name";
                        worksheet.Cell(1, 3).Value = isArabic ? "معرف الطلب" : "Order ID";
                        worksheet.Cell(1, 4).Value = isArabic ? "معرف المنتج" : "Product ID";
                        worksheet.Cell(1, 5).Value = isArabic ? "اسم المنتج" : "Product Name";
                        worksheet.Cell(1, 6).Value = isArabic ? "سعر البيع" : "Sale Price";
                        worksheet.Cell(1, 7).Value = isArabic ? "سعر التكلفة" : "Cost Price";
                        worksheet.Cell(1, 8).Value = isArabic ? "الكمية" : "Quantity";
                        worksheet.Cell(1, 9).Value = isArabic ? "الإجمالي" : "Total";

                        int row = 2;
                        foreach (var sale in rows)
                        {
                            worksheet.Cell(row, 1).Value = sale.CategoryId;
                            worksheet.Cell(row, 2).Value = isArabic ? sale.CNameArb : sale.CName;
                            worksheet.Cell(row, 3).Value = sale.OrderId;
                            worksheet.Cell(row, 4).Value = sale.ProductId;
                            worksheet.Cell(row, 5).Value = isArabic ? sale.NameArb : sale.Name;
                            worksheet.Cell(row, 6).Value = sale.Price;
                            worksheet.Cell(row, 7).Value = sale.CostPrice;
                            worksheet.Cell(row, 8).Value = sale.Quantity;
                            worksheet.Cell(row, 9).Value = sale.TotalAmount;
                            row++;
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            wb.SaveAs(ms);
                            var fileName = isArabic
                                ? $"المبيعات_اليومية_{DateTime.Now:yyyy-MM-dd}.xlsx"
                                : $"CategorySaleReport_{DateTime.Now:yyyy-MM-dd}.xlsx";

                            return File(ms.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                        fileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Content("An error occurred while generating the report: " + ex.Message);
            }
        }
        public class CategoryReportModel
        {

            public int CategoryId { get; set; }
            public string? CName { get; set; }
            public string? CNameArb { get; set; }


            public int ProductId { get; set; }
            public string? Name { get; set; }
            public string? NameArb { get; set; }


            public int CustomerId { get; set; }
            public string? FullName { get; set; }
            public string? FullNameArb { get; set; }


            public int OrderId { get; set; }
            public int? Quantity { get; set; }
            public string? OrderStatusName { get; set; }
            public string? OrderStatusNameArb { get; set; }
            public string? PaymentType { get; set; }
            public DateTime? OrderDate { get; set; }


            public decimal? TotalAmount { get; set; }
            public decimal? DiscountAmount { get; set; }
            public decimal? CostPrice { get; set; }
            public decimal? Price { get; set; }
        }
        #endregion

        #region Customers Report


        public class CustomerReportRow
        {
            public int UserId { get; set; }
            public string? FullName { get; set; } = "";
            public string? FullNameArb { get; set; } = "";
            public string? Email { get; set; } = "";
            public string? Phone { get; set; }

            public bool IsActive { get; set; }
            public DateTime? CreatedAt { get; set; }
        }



        public IActionResult Customers()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> CustomersReport(DateTime? from, DateTime? to, bool? isActive, string? keyword, string format = "PDF")
        {
            try
            {
                var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                bool isArabic = culture == "ar";

                var rows = await _context.Set<CustomerReportRow>()
                    .FromSqlRaw("EXEC dbo.sp_CustomerList @From = {0}, @To = {1}, @IsActive = {2}, @Keyword = {3}",
                        from, to, isActive, keyword ?? "")
                    .ToListAsync();

                if (format == "PDF")
                {
                    if (isArabic)
                    {
                        foreach (var r in rows)
                        {
                            r.FullName = r.FullNameArb;
                        }
                    }

                    var rdlcFile = isArabic ? "CustomerList_Ar.rdlc" : "CustomerList.rdlc";
                    var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                    using var defStream = System.IO.File.OpenRead(rdlcPath);
                    var report = new LocalReport { EnableExternalImages = true };
                    report.LoadReportDefinition(defStream);

                    report.DataSources.Add(new ReportDataSource("DataSet1", rows));

                    var company = _context.CompanyDetails.FromSqlRaw("SELECT TOP 1 * FROM CompanyDetails").FirstOrDefault();

                    string companyName = isArabic ? company.CompanyNameArabic : company.CompanyName;
                    string companyAddress = isArabic ? company.AddressArabic : company.Address;
                    string phone = company.Phone;

                    string imagePath = "";
                    if (company.Logo != null && company.Logo.Length > 0)
                    {
                        var folder = Path.Combine(_env.WebRootPath, "Temp");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var fileName2 = "logo.png";
                        imagePath = Path.Combine(folder, fileName2);
                        System.IO.File.WriteAllBytes(imagePath, company.Logo);

                        imagePath = new Uri(imagePath).AbsoluteUri;
                    }

                    var pFrom = from?.ToString("dd-mm-yyyy") ?? "";
                    var pTo = to?.ToString("dd-mm-yyyy") ?? "";
                    var pStat = isActive.HasValue ? (isActive.Value ? (isArabic ? "نشط" : "Active") : (isArabic ? "غير نشط" : "Inactive")) : (isArabic ? "الكل" : null);
                    var pKey = keyword ?? "";
                    var pGenAt = DateTime.Now.ToString("dd-MMM-yyyy HH:mm");

                    report.SetParameters(new[]
                    {
                new ReportParameter("pFrom", pFrom),
                new ReportParameter("pTo", pTo),
                new ReportParameter("pKeyword", pKey),
                new ReportParameter("pGeneratedAt", pGenAt),
                new ReportParameter("pStatus", pStat),
                new ReportParameter("CompanyName", companyName ?? ""),
                new ReportParameter("Address", companyAddress ?? ""),
                new ReportParameter("Phone", phone ?? ""),
                new ReportParameter("Logo", imagePath ?? ""),
                new ReportParameter("UserName", "Admin")
            });

                    var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    {
                        "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                        "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                        _ => ("PDF", "application/pdf", "pdf")
                    };

                    byte[] bytes = report.Render(renderFormat);
                    var fileName = $"CustomerList_{DateTime.Now:yyyyMMddHHmm}.{ext}";

                    Response.Headers.Add("Content-Disposition", "inline; filename=" + fileName);
                    return File(bytes, mime);
                }
                else
                {
                    using (XLWorkbook wb = new XLWorkbook())
                    {
                        var worksheet = wb.Worksheets.Add(isArabic ? "قائمة العملاء" : "Customer List");

                        // 🔹 Header row
                        worksheet.Cell(1, 1).Value = isArabic ? "معرف العميل" : "Customer Id";
                        worksheet.Cell(1, 2).Value = isArabic ? "الاسم الكامل" : "Full Name";
                        worksheet.Cell(1, 3).Value = isArabic ? "البريد الإلكتروني" : "Email";
                        worksheet.Cell(1, 4).Value = isArabic ? "الهاتف" : "Phone";
                        worksheet.Cell(1, 5).Value = isArabic ? "الحالة" : "Status";
                        worksheet.Cell(1, 6).Value = isArabic ? "الحالة" : "Created At";

                        int row = 2;
                        foreach (var cust in rows)
                        {
                            worksheet.Cell(row, 1).Value = cust.UserId;
                            worksheet.Cell(row, 2).Value = isArabic ? cust.FullNameArb : cust.FullName;
                            worksheet.Cell(row, 3).Value = cust.Email;
                            worksheet.Cell(row, 4).Value = cust.Phone;
                            worksheet.Cell(row, 5).Value = isArabic
                                ? (cust.IsActive ? "نشط" : "غير نشط")
                                : (cust.IsActive ? "Active" : "Inactive");
                            worksheet.Cell(row, 6).Value = cust.CreatedAt;
                            row++;
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            wb.SaveAs(ms);

                            var fileName = isArabic
                                ? $"قائمة_العملاء_{DateTime.Now:yyyy-MM-dd}.xlsx"
                                : $"CustomerList_{DateTime.Now:yyyy-MM-dd}.xlsx";

                            return File(ms.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                        fileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Content("An error occurred while generating the report: " + ex.Message);
            }
        }

        #endregion

        #region StockReport
        public ActionResult StockReport()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            bool isArabic = culture.TextInfo.IsRightToLeft;
            return View();
        }
        public async Task<IActionResult> StockReportPrint(string? productName, string? status, string? searchCode, string format = "PDF")
        {
            try
            {

                var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                bool isArabic = culture == "ar";

                var rows = await _context.Set<ProductStockViewModel>()
    .FromSqlRaw(
        "EXEC dbo.sp_StockReport @ProductName = {0}, @Status = {1}, @Ds_Barcode = {2}, @Ds_ProdID = {3}",
        (object?)productName ?? DBNull.Value,
        (object?)status ?? DBNull.Value,
        (object?)searchCode ?? DBNull.Value,
        (object?)searchCode ?? DBNull.Value
    )
    .ToListAsync();

                if (rows == null || !rows.Any())
                    return Content("No data available for the report.");

                if (format == "PDF")
                {
                    if (isArabic)
                    {
                        foreach (var r in rows)
                        {
                            r.ProductName = r.ProductNameArb;
                        }
                    }


                    var rdlcFile = isArabic ? "StockReport_Ar.rdlc" : "StockReport.rdlc";
                    var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                    using var defStream = System.IO.File.OpenRead(rdlcPath);
                    var report = new LocalReport();
                    report.EnableExternalImages = true;
                    report.LoadReportDefinition(defStream);


                    report.DataSources.Add(new ReportDataSource("DataSet1", rows));


                    var company = _context.CompanyDetails
                        .FromSqlRaw("SELECT TOP 1 * FROM CompanyDetails")
                        .FirstOrDefault();

                    string companyName = isArabic ? company.CompanyNameArabic : company.CompanyName;
                    string companyAddress = isArabic ? company.AddressArabic : company.Address;
                    string phone = company.Phone;

                    string imagePath = "";
                    if (company.Logo != null && company.Logo.Length > 0)
                    {
                        var folder = Path.Combine(_env.WebRootPath, "Temp");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var fileName2 = "logo.png";
                        imagePath = Path.Combine(folder, fileName2);
                        System.IO.File.WriteAllBytes(imagePath, company.Logo);

                        imagePath = new Uri(imagePath).AbsoluteUri;
                    }


                    var pProd = productName ?? "";
                    var code = searchCode ?? "";
                    var pStat = string.IsNullOrEmpty(status) ? "" : status;
                    var pGenAt = DateTime.Now.ToString("dd-mm-yyyy HH:mm");

                    report.SetParameters(new[]
                    {
            new ReportParameter("pProductName", pProd),
            new ReportParameter("pStatus", pStat),
            new ReportParameter("pcode", code),
            new ReportParameter("pGeneratedAt", pGenAt),
            new ReportParameter("CompanyName", companyName ?? ""),
            new ReportParameter("Address", companyAddress ?? ""),
            new ReportParameter("Phone", phone ?? ""),
            new ReportParameter("Logo", imagePath ?? ""),
            new ReportParameter("UserName", "Admin")
        });


                    //var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    //{
                    //    "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                    //    "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                    //    _ => ("PDF", "application/pdf", "pdf")
                    //};

                    //byte[] bytes = report.Render(renderFormat);
                    //var fileName = $"StockReport_{DateTime.Now:yyyyMMddHHmm}.{ext}";

                    //return File(bytes, mime, fileName);

                    var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    {
                        "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                        "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                        _ => ("PDF", "application/pdf", "pdf")
                    };

                    byte[] bytes = report.Render(renderFormat);


                    var fileName = $"StockReport_{DateTime.Now:yyyyMMddHHmm}.{ext}";


                    Response.Headers.Add("Content-Disposition", "inline; filename=" + fileName);

                    return File(bytes, mime);
                }
                else
                {
                    using (XLWorkbook wb = new XLWorkbook())
                    {
                        var worksheet = wb.Worksheets.Add(isArabic ? "المبيعات اليومية" : "DaywiseSale");

                        // 🔹 Header row (Culture wise)
                        worksheet.Cell(1, 1).Value = isArabic ? "معرّف الفئة" : "Product ID";
                        worksheet.Cell(1, 2).Value = isArabic ? "اسم الفئة" : "Product Name";
                        worksheet.Cell(1, 3).Value = isArabic ? "إجمالي المخزون" : "Total Stock";
                        worksheet.Cell(1, 4).Value = isArabic ? "إجمالي المخزون المباع" : "Total Sold Stock";
                        worksheet.Cell(1, 5).Value = isArabic ? "الكمية المتبقية" : "Remaining Qty";
                        worksheet.Cell(1, 6).Value = isArabic ? "الحالة" : "Status";
                        worksheet.Cell(1, 7).Value = isArabic ? "رمز العنصر" : "Ds_ProdID";
                        worksheet.Cell(1, 8).Value = isArabic ? "باركود" : "Ds_Barcode";



                        int row = 2;
                        foreach (var sale in rows)
                        {
                            worksheet.Cell(row, 1).Value = sale.ProductId;
                            worksheet.Cell(row, 2).Value = isArabic ? sale.ProductNameArb : sale.ProductName;
                            worksheet.Cell(row, 3).Value = sale.TotalStock;
                            worksheet.Cell(row, 4).Value = sale.Totalsoldstock;
                            worksheet.Cell(row, 5).Value = sale.RemainingQty;
                            worksheet.Cell(row, 6).Value = sale.Status;
                            worksheet.Cell(row, 7).Value = sale.Ds_ProdID;
                            worksheet.Cell(row, 8).Value = sale.Ds_Barcode;

                            row++;
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            wb.SaveAs(ms);
                            var fileName = isArabic
                                ? $"المبيعات_اليومية_{DateTime.Now:yyyy-MM-dd}.xlsx"
                                : $"StockReport_{DateTime.Now:yyyy-MM-dd}.xlsx";

                            return File(ms.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                        fileName);
                        }

                    }
                }
            }

            catch (Exception ex)
            {
                return Content("An error occurred while generating the report: " + ex.Message);
            }
        }

        #endregion

        #region ItemReport
        public IActionResult ItemReport()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ItemfileReport(string? name, decimal? salePrice, string? status,
                                          bool showCategory = false, bool showStock = false,
                                          string format = "PDF")
        {
            try
            {
                var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                bool isArabic = culture == "ar";

                var rows = await _context.Set<ItemFileReportRow>()
                    .FromSqlRaw("EXEC dbo.sp_ItemfileReport @Name = {0}, @SalePrice = {1}, @Status = {2}",
                        name ?? "", salePrice, status ?? "")
                    .ToListAsync();

                if (format == "PDF")
                {
                    if (isArabic)
                    {
                        foreach (var r in rows)
                        {
                            r.Name = r.NameArb;
                            r.CName = r.CNameArb;
                        }
                    }

                    var rdlcFile = isArabic ? "ItemFileReport_Ar.rdlc" : "ItemFileReport.rdlc";
                    var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                    using var defStream = System.IO.File.OpenRead(rdlcPath);
                    var report = new LocalReport();
                    report.EnableExternalImages = true;
                    report.LoadReportDefinition(defStream);

                    report.DataSources.Add(new ReportDataSource("DataSet1", rows));

                    var company = _context.CompanyDetails.FromSqlRaw("SELECT TOP 1 * FROM CompanyDetails").FirstOrDefault();

                    string companyName = isArabic ? company.CompanyNameArabic : company.CompanyName;
                    string companyAddress = isArabic ? company.AddressArabic : company.Address;
                    string phone = company.Phone;

                    string imagePath = "";
                    if (company.Logo != null && company.Logo.Length > 0)
                    {
                        var folder = Path.Combine(_env.WebRootPath, "Temp");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var fileName2 = "logo.png";
                        imagePath = Path.Combine(folder, fileName2);
                        System.IO.File.WriteAllBytes(imagePath, company.Logo);

                        imagePath = new Uri(imagePath).AbsoluteUri;
                    }

                    var pName = name ?? null;
                    var pSalePrice = salePrice?.ToString("0.00") ?? null;
                    var pStat = string.IsNullOrEmpty(status) ? null : status;
                    var pGenAt = DateTime.Now.ToString("dd-MMM-yyyy HH:mm");

                    report.SetParameters(new[]
                    {
                new ReportParameter("pName", pName),
                new ReportParameter("pSalePrice", pSalePrice),
                new ReportParameter("pStatus", pStat),
                new ReportParameter("pGeneratedAt", pGenAt),
                new ReportParameter("CompanyName", companyName ?? ""),
                new ReportParameter("Address", companyAddress ?? ""),
                new ReportParameter("Phone", phone ?? ""),
                new ReportParameter("Logo", imagePath ?? ""),
                new ReportParameter("UserName", "Admin"),
                new ReportParameter("ShowCategory", showCategory ? "true" : "false"),
                new ReportParameter("ShowStock", showStock ? "true" : "false")
            });

                    //var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    //{
                    //    "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                    //    "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                    //    _ => ("PDF", "application/pdf", "pdf")
                    //};

                    //byte[] bytes = report.Render(renderFormat);
                    //var fileName = $"ItemfileReport_{DateTime.Now:yyyyMMddHHmm}.{ext}";

                    //return File(bytes, mime, fileName);
                    var (renderFormat, mime, ext) = format?.ToUpperInvariant() switch
                    {
                        "XLSX" or "EXCEL" => ("EXCELOPENXML", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                        "DOCX" or "WORD" => ("WORDOPENXML", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
                        _ => ("PDF", "application/pdf", "pdf")
                    };

                    byte[] bytes = report.Render(renderFormat);

                    var fileName = $"ItemFileReport_{DateTime.Now:yyyyMMddHHmm}.{ext}";
                    Response.Headers.Add("Content-Disposition", "inline; filename=" + fileName);

                    return File(bytes, mime);
                }
                else
                {
                    using (XLWorkbook wb = new XLWorkbook())
                    {
                        var worksheet = wb.Worksheets.Add(isArabic ? "المبيعات اليومية" : "DaywiseSale");

                        // 🔹 Header row (Culture wise)
                        worksheet.Cell(1, 1).Value = isArabic ? "معرّف المنتج" : "Product ID";
                        worksheet.Cell(1, 2).Value = isArabic ? "اسم المنتج" : "Product Name";
                        worksheet.Cell(1, 3).Value = isArabic ? "اسم الفئة" : "Category Name";
                        worksheet.Cell(1, 4).Value = isArabic ? "المخزون" : "Stock";
                        worksheet.Cell(1, 5).Value = isArabic ? "سعر التكلفة" : "Cost Price";
                        worksheet.Cell(1, 6).Value = isArabic ? "سعر البيع" : "Sale Price";
                        worksheet.Cell(1, 7).Value = isArabic ? "الحالة" : "Status";

                        int row = 2;
                        foreach (var sale in rows)
                        {
                            worksheet.Cell(row, 1).Value = sale.ProductId;
                            worksheet.Cell(row, 2).Value = isArabic ? sale.NameArb : sale.Name;
                            worksheet.Cell(row, 3).Value = isArabic ? sale.CNameArb : sale.CName;
                            worksheet.Cell(row, 4).Value = sale.Stock;
                            worksheet.Cell(row, 5).Value = sale.CoastPrice;
                            worksheet.Cell(row, 6).Value = sale.Price;
                            worksheet.Cell(row, 7).Value = sale.IsActive;
                            row++;
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            wb.SaveAs(ms);
                            var fileName = isArabic
                                ? $"تقرير_الملفات_{DateTime.Now:yyyy-MM-dd}.xlsx"
                                : $"ItemFileReport_{DateTime.Now:yyyy-MM-dd}.xlsx";

                            return File(ms.ToArray(),
                                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                        fileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Agar error aaye to ek simple text response ya error page
                return Content($"Error generating report: {ex.Message}");
            }
        }



        public class ItemFileReportRow
        {

            public int ProductId { get; set; }
            public string Name { get; set; }
            public string NameArb { get; set; }
            public int Stock { get; set; }
            public decimal CoastPrice { get; set; }
            public decimal Price { get; set; }
            public string CName { get; set; }
            public string CNameArb { get; set; }
            public string IsActive { get; set; }
        }

        #endregion

        #region Suggetion Product
        [HttpGet]
        public async Task<JsonResult> AutoCompleteSql(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new List<string>());

            IQueryable<Products> queryable = _context.Products;


            bool isArabic = term.Any(c => c >= 0x0600 && c <= 0x06FF);

            if (isArabic)
            {
                queryable = queryable.Where(c => c.NameArb.Contains(term));
            }
            else
            {
                queryable = queryable.Where(c => c.Name.Contains(term));
            }
            var suggestions = await queryable
                .Select(c => isArabic ? c.NameArb : c.Name)
                .Take(10)
                .ToListAsync();
            return Json(suggestions);



        }
        #endregion

        #region Order Invoice

        public class OrderProductViewModel
        {
            //public int ProductId { get; set; }

            public string ProductName { get; set; }

            public string ProductNameArb { get; set; }

            public int Quantity { get; set; }

            public decimal Price { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal VatAmount { get; set; }
            public decimal DeliveryCharge { get; set; }
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public int? ShareholderNo { get; set; }
            public int? ProductId { get; set; }
            public string Phone { get; set; }
            public string AddPhone { get; set; }
            public string? HouseNo { get; set; }
            public string? Street { get; set; }
            public string? Block { get; set; }
            public string? Area { get; set; }
            public string? Landmark { get; set; }
            public string? Floor { get; set; }
            public string? OrderstatusName { get; set; }
            public string? OrderStatusNameArb { get; set; }
            public string? PaymentStatusNameArb { get; set; }
            public string? PaymentstatusName { get; set; }
            public string? PaymentType { get; set; }
            public string? couponcode { get; set; }
            public string? CreatedDate { get; set; }
            public string? AreaNameArb { get; set; }
            public string? AreaName { get; set; }
            public string? BlockNameArb { get; set; }
            public string? BlockName { get; set; }

        }

        public async Task<IActionResult> OrderInvoicePrint(int orderId, string format = "PDF")
        {
            if (!User.IsInRole("Admin") &&
                (!User.IsInRole("Customer") || !int.TryParse(User.FindFirst("UserId")?.Value, out var customerId) ||
                 !await _context.Orders.AnyAsync(o => o.OrderId == orderId && o.CustomerId == customerId)))
                return Forbid();
            try
            {
                var culture = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
                bool isArabic = culture == "ar";

                // ✅ Get order products using new stored procedure
                //var rows = await _context.Set<OrderProductViewModel>()
                //    .FromSqlRaw("EXEC dbo.sp_GetOrderProducts @OrderId = {0}, @language = {1}", orderId, culture)
                //    .ToListAsync();
                var rows = await _context.Set<OrderProductViewModel>()
    .FromSqlRaw(
        "EXEC dbo.sp_GetOrderProducts @OrderId = {0}",
        orderId
    )
    .ToListAsync();

                if (rows == null || !rows.Any())
                    return Content("No data available for the order.");

                if (format.ToUpper() == "PDF")
                {
                    if (isArabic)
                    {
                        foreach (var r in rows)
                        {
                            r.ProductName = r.ProductNameArb;
                        }
                    }
                    // ✅ Load RDLC
                    var rdlcFile = isArabic ? "OrderInvoiceReport_arbic.rdlc" : "OrderInvoiceReport.rdlc";
                    var rdlcPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                    using var defStream = System.IO.File.OpenRead(rdlcPath);
                    var report = new LocalReport();
                    report.EnableExternalImages = true;
                    report.LoadReportDefinition(defStream);

                    report.DataSources.Add(new ReportDataSource("DataSet1", rows));

                    // ✅ Company details
                    var company = _context.CompanyDetails
                        .FromSqlRaw("SELECT TOP 1 * FROM CompanyDetails")
                        .FirstOrDefault();

                    string companyName = isArabic ? company.CompanyNameArabic : company.CompanyName;
                    string companyAddress = isArabic ? company.AddressArabic : company.Address;
                    string phone = company.Phone;

                    string imagePath = "";
                    if (company.Logo != null && company.Logo.Length > 0)
                    {
                        var folder = Path.Combine(_env.WebRootPath, "Temp");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var fileName2 = "logo.png";
                        imagePath = Path.Combine(folder, fileName2);
                        System.IO.File.WriteAllBytes(imagePath, company.Logo);

                        imagePath = new Uri(imagePath).AbsoluteUri;
                    }

                    var pGenAt = DateTime.Now.ToString("dd-MM-yyyy HH:mm");

                    report.SetParameters(new[]
                    {
                new ReportParameter("pGeneratedAt", pGenAt),
                new ReportParameter("CompanyName", companyName ?? ""),
                new ReportParameter("Address", companyAddress ?? ""),
                new ReportParameter("Phone", phone ?? ""),
                new ReportParameter("Logo", imagePath ?? ""),
                new ReportParameter("UserName", "Admin")
            });

                    // ✅ Render PDF
                    var (renderFormat, mime, ext) = ("PDF", "application/pdf", "pdf");
                    byte[] bytes = report.Render(renderFormat);

                    var fileName = $"OrderInvoice_{DateTime.Now:yyyyMMddHHmm}.{ext}";
                    Response.Headers.Add("Content-Disposition", "inline; filename=" + fileName);

                    return File(bytes, mime);
                }
                else
                {
                    return Content("Only PDF format is supported now.");
                }
            }
            catch (Exception ex)
            {
                return Content("An error occurred while generating the report: " + ex.Message);
            }
        }


        #endregion
    }
}
