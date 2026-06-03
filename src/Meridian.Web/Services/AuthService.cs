using Meridian.Domain.Common;
using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Meridian.Infrastructure.Identity;
using Meridian.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Meridian.Web.Services;

public class AuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthService> _logger;
    private readonly EmailService _emailService;
    private readonly IConfiguration _config;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IUnitOfWork uow,
        ILogger<AuthService> logger,
        EmailService emailService,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _uow = uow;
        _logger = logger;
        _emailService = emailService;
        _config = config;
    }

    public async Task<Result> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
            return Result.Failure("Invalid credentials.");

        var result = await _signInManager.PasswordSignInAsync(
            user, password, isPersistent: true, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("User {Email} logged in", email);
            return Result.Success();
        }

        if (result.IsLockedOut)
            return Result.Failure("Account is temporarily locked. Please try again later.");

        if (result.IsNotAllowed)
            return Result.Failure("Please verify your email address before signing in.");

        return Result.Failure("Invalid credentials.");
    }

    public async Task<Result> RegisterAsync(
        string orgName,
        string firstName,
        string lastName,
        string email,
        string password)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
            return Result.Failure("An account with this email already exists.");

        // Create tenant
        var slug = GenerateSlug(orgName);
        var tenant = Tenant.Create(orgName, slug, SubscriptionTier.Starter, 1);
        await _uow.Tenants.AddAsync(tenant);
        await _uow.SaveChangesAsync();

        // Create owner user
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenant.Id,
            Role = UserRole.Owner,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(errors);
        }

        // Send confirmation email
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var baseUrl = _config["BaseUrl"] ?? "https://interlimen.com";
        var confirmationLink = $"{baseUrl}/account/confirm-email?userId={user.Id}&token={encodedToken}";

        await _emailService.SendConfirmationEmailAsync(email, firstName, confirmationLink);
        
         // Send "New Signup" email to me
         await _emailService.SendNewUserNotificationAsync(orgName, firstName, lastName, email);

        _logger.LogInformation("New tenant {OrgName} registered by {Email}", orgName, email);
        return Result.Success();
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    private static string GenerateSlug(string name)
    {
        var base_slug = System.Text.RegularExpressions.Regex
            .Replace(name.ToLower().Trim(), @"[^a-z0-9]+", "-")
            .Trim('-');
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{base_slug}-{suffix}";
    }
}