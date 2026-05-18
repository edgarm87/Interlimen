using Meridian.Application;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Persistence;
using Meridian.Web.Components;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddApplicationServices();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Meridian.Infrastructure.Persistence.AppDbContext>());
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Meridian.Web.Services.AuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Login endpoint — handles cookie auth outside Blazor circuit
app.MapPost("/auth/login", async (HttpContext ctx,
    SignInManager<Meridian.Infrastructure.Identity.AppUser> signInManager,
    UserManager<Meridian.Infrastructure.Identity.AppUser> userManager) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var user = await userManager.FindByEmailAsync(email);
    if (user == null || !user.IsActive)
        return Results.Redirect($"/account/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");

    var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

    if (result.Succeeded)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/dashboard" : returnUrl);
    }

    if (result.IsLockedOut)
        return Results.Redirect($"/account/login?error=locked&returnUrl={Uri.EscapeDataString(returnUrl)}");

    return Results.Redirect($"/account/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
});

// Logout endpoint
app.MapPost("/auth/logout", async (HttpContext ctx,
    SignInManager<Meridian.Infrastructure.Identity.AppUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/account/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();