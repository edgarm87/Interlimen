using MediatR;
using Microsoft.AspNetCore.Identity;
using Meridian.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Meridian.Infrastructure.Persistence;

namespace Meridian.Infrastructure.Features.Settings;

public record TeamMemberDto(
    string Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record TenantInfoDto(Guid Id, string Name, int MaxSeats, string Status);

public record GetTeamMembers(Guid TenantId) : IRequest<List<TeamMemberDto>>;
public record GetTenantInfo(Guid TenantId) : IRequest<TenantInfoDto?>;
public record InviteTeamMember(Guid TenantId, string FirstName, string LastName, string Email, string Password, UserRole Role) : IRequest<(bool Success, string? Error)>;
public record DeactivateTeamMember(string UserId, Guid TenantId) : IRequest<(bool Success, string? Error)>;

public class GetTeamMembersHandler : IRequestHandler<GetTeamMembers, List<TeamMemberDto>>
{
    private readonly AppDbContext _ctx;
    public GetTeamMembersHandler(AppDbContext ctx) => _ctx = ctx;

    public async Task<List<TeamMemberDto>> Handle(GetTeamMembers request, CancellationToken ct)
    {
        return await _ctx.Users
            .Where(u => u.TenantId == request.TenantId)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.LastName)
            .Select(u => new TeamMemberDto(
                u.Id, u.FullName, u.Email ?? "",
                u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt))
            .ToListAsync(ct);
    }
}

public class GetTenantInfoHandler : IRequestHandler<GetTenantInfo, TenantInfoDto?>
{
    private readonly AppDbContext _ctx;
    public GetTenantInfoHandler(AppDbContext ctx) => _ctx = ctx;

    public async Task<TenantInfoDto?> Handle(GetTenantInfo request, CancellationToken ct)
    {
        var tenant = await _ctx.Tenants.FindAsync(new object[] { request.TenantId }, ct);
        if (tenant == null) return null;
        return new TenantInfoDto(tenant.Id, tenant.Name, tenant.MaxSeats, tenant.Status.ToString());
    }
}

public class InviteTeamMemberHandler : IRequestHandler<InviteTeamMember, (bool Success, string? Error)>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _ctx;

    public InviteTeamMemberHandler(UserManager<AppUser> userManager, AppDbContext ctx)
    {
        _userManager = userManager;
        _ctx = ctx;
    }

    public async Task<(bool Success, string? Error)> Handle(InviteTeamMember request, CancellationToken ct)
    {
        var tenant = await _ctx.Tenants.FindAsync(new object[] { request.TenantId }, ct);
        if (tenant == null) return (false, "Tenant not found.");

        var currentCount = await _ctx.Users.CountAsync(u => u.TenantId == request.TenantId && u.IsActive, ct);
        if (!tenant.CanAddSeat(currentCount))
            return (false, $"Your plan allows {tenant.MaxSeats} seats. Upgrade to add more users.");

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null) return (false, "A user with this email already exists.");

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = request.TenantId,
            Role = request.Role,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        return (true, null);
    }
}

public class DeactivateTeamMemberHandler : IRequestHandler<DeactivateTeamMember, (bool Success, string? Error)>
{
    private readonly AppDbContext _ctx;
    public DeactivateTeamMemberHandler(AppDbContext ctx) => _ctx = ctx;

    public async Task<(bool Success, string? Error)> Handle(DeactivateTeamMember request, CancellationToken ct)
    {
        var user = await _ctx.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == request.TenantId, ct);
        if (user == null) return (false, "User not found.");
        if (user.Role == UserRole.Owner) return (false, "Cannot deactivate the account owner.");
        user.IsActive = !user.IsActive;
        await _ctx.SaveChangesAsync(ct);
        return (true, null);
    }
}
