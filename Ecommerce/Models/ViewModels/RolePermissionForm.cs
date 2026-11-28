using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels;

public sealed class RolePermissionForm
{
    [Range(1, int.MaxValue)]
    public int RoleId { get; set; }
    public List<PermissionRow> Rows { get; set; } = new();

    public bool HasExactPages(IEnumerable<int> pageIds)
    {
        var expected = pageIds.ToHashSet();
        return Rows != null && expected.Count > 0 && Rows.Count == expected.Count &&
            Rows.Select(r => r.PageId).ToHashSet().SetEquals(expected);
    }
}

public sealed class PermissionRow
{
    public int PageId { get; set; }
    public string? PageName { get; set; }
    public bool ListPage { get; set; }
    public bool CreatePage { get; set; }
    public bool UpdatePage { get; set; }
    public bool DeletePage { get; set; }
    public bool ReportView { get; set; }
    public bool ReportPrint { get; set; }

    public bool HasRights => ListPage || CreatePage || UpdatePage || DeletePage || ReportView || ReportPrint;

    public static PermissionRow From(PageMaster page, RolePermission? permission, bool arabic) => new()
    {
        PageId = page.PageId,
        PageName = arabic ? page.PageNameArb ?? page.PageNameEng : page.PageNameEng,
        ListPage = permission?.ListPage ?? false,
        CreatePage = permission?.CreatePage ?? false,
        UpdatePage = permission?.UpdatePage ?? false,
        DeletePage = permission?.DeletePage ?? false,
        ReportView = permission?.ReportView ?? false,
        ReportPrint = permission?.ReportPrint ?? false
    };

    public bool Matches(RolePermission permission) =>
        ListPage == permission.ListPage && CreatePage == permission.CreatePage &&
        UpdatePage == permission.UpdatePage && DeletePage == permission.DeletePage &&
        ReportView == permission.ReportView && ReportPrint == permission.ReportPrint;

    public void ApplyTo(RolePermission permission)
    {
        permission.ListPage = ListPage;
        permission.CreatePage = CreatePage;
        permission.UpdatePage = UpdatePage;
        permission.DeletePage = DeletePage;
        permission.ReportView = ReportView;
        permission.ReportPrint = ReportPrint;
    }
}
