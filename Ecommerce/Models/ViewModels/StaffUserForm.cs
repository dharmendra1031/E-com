using System.ComponentModel.DataAnnotations;

namespace EcommerceWebApplication.Models.ViewModels;

public class StaffUserForm
{
    public int UserId { get; set; }
    [Required, MaxLength(100)] public string? FullName { get; set; }
    [Required, MaxLength(100)] public string? FullNameArb { get; set; }
    [Required, MaxLength(150), EmailAddress] public string? Email { get; set; }
    [Required, MaxLength(12), RegularExpression(@"^\+?[0-9]+$")] public string? Phone { get; set; }
    [DataType(DataType.Password)] public string? PasswordHash { get; set; }
    [Range(1, int.MaxValue)] public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;

    public static StaffUserForm FromUser(User user) => new()
    {
        UserId = user.UserId, FullName = user.FullName, FullNameArb = user.FullNameArb,
        Email = user.Email, Phone = user.Phone, RoleId = user.RoleId, IsActive = user.IsActive
    };

    public void ApplyTo(User user, int actorId)
    {
        user.FullName = FullName;
        user.FullNameArb = FullNameArb;
        user.Email = Email;
        user.Phone = Phone!;
        user.RoleId = RoleId;
        user.IsActive = IsActive;
        user.UpdatedBy = actorId;
        user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(PasswordHash))
            user.PasswordHash = Helper.StaffPassword.Hash(PasswordHash);
    }
}
