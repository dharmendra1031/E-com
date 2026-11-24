using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models
{
    public class RolePermission
    {
        [Key]
        public int RolePermissionId { get; set; }
        public int RoleId { get; set; }
        [ForeignKey("PageMaster")]
        public int PageId { get; set; }

        public bool CreatePage { get; set; }
        public bool DeletePage { get; set; }
        public bool UpdatePage { get; set; }
        public bool ListPage { get; set; }
        public bool ReportView { get; set; }
        public bool ReportPrint { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int? CreatedBy { get; set; }
        public int? UpdateBy { get; set; }

        [NotMapped]
        public List<int> PageIds { get; set; }
        
    }
}
