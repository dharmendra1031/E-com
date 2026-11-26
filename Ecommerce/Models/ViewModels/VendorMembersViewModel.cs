namespace EcommerceWebApplication.Models.ViewModels;

public class VendorMembersViewModel
{
    public int VendorId { get; set; }
    public string VendorName { get; set; } = "";
    public string? Search { get; set; }
    public int Page { get; set; }
    public bool HasNext { get; set; }
    public List<MemberRow> Members { get; set; } = new();
    public List<MemberRow> Candidates { get; set; } = new();

    public class MemberRow
    {
        public int UserId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsEligible { get; set; }
    }
}
