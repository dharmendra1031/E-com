using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels
{
    public class PermissionListViewModel
    {
        [Key]
        public int RolePermissionId { get; set; }
        public string? RoleName { get; set; }
        public string ? PageName { get; set; }
        public int CreatePage { get; set; }
        public int UpdatePage { get; set; }
        public int DeletePage { get; set; }
        public int ListPage { get; set; }
        public int ReportView { get; set; }
        public int ReportPrint { get; set; }
        public DateTime ? CreatedAt { get; set; }
    }
}
