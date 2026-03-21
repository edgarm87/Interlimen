using Microsoft.AspNetCore.Identity;

namespace Meridian.Infrastructure.Identity;

/// <summary>
/// AppUser extends ASP.NET Core Identity's built-in IdentityUser — so we get email, password hashing,
/// lockout, and claims for free, and we bolt on our own fields: which tenant they belong to, their name,
/// their role within the firm, and whether they're active.
/// </summary>
public class AppUser : IdentityUser
{
    public Guid TenantId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string FullName => $"{FirstName} {LastName}";
    public UserRole Role { get; set; } = UserRole.Member;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// The three roles map directly to the SaaS seat model —
/// Owner manages billing, Admin manages team members, Member is a standard attorney or paralegal seat.
/// </summary>
public enum UserRole
{
    Owner,
    Admin,
    Member
}