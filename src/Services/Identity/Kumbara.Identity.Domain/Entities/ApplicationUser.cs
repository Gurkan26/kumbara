namespace Kumbara.Identity.Domain.Entities;

using Kumbara.Identity.Domain.Common;

public class ApplicationUser : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string Role { get; set; } = "User";
}
