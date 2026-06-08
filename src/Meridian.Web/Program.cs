using Meridian.Application;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Persistence;
using Meridian.Web.Components;
using Meridian.Web.Services;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using Resend;

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
builder.Services.AddResend(options =>
    options.ApiToken = builder.Configuration["Resend:ApiKey"] 
                       ?? Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? "");
builder.Services.AddScoped<EmailService>();

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
        user.LastLoginIp = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                               ?.Split(',').First().Trim()
                           ?? ctx.Connection.RemoteIpAddress?.ToString();
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

// Sitemap
app.MapGet("/sitemap.xml", (HttpContext ctx) =>
{
    var baseUrl = "https://interlimen.com";
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var xml = $"""
               <?xml version="1.0" encoding="UTF-8"?>
               <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                   <url>
                       <loc>{baseUrl}/</loc>
                       <lastmod>{today}</lastmod>
                       <changefreq>weekly</changefreq>
                       <priority>1.0</priority>
                   </url>
                   <url>
                       <loc>{baseUrl}/pricing</loc>
                       <lastmod>{today}</lastmod>
                       <changefreq>monthly</changefreq>
                       <priority>0.8</priority>
                   </url>
                   <url>
                       <loc>{baseUrl}/terms</loc>
                       <lastmod>{today}</lastmod>
                       <changefreq>yearly</changefreq>
                       <priority>0.3</priority>
                   </url>
                   <url>
                       <loc>{baseUrl}/privacy</loc>
                       <lastmod>{today}</lastmod>
                       <changefreq>yearly</changefreq>
                       <priority>0.3</priority>
                   </url>
               </urlset>
               """;
    return Results.Content(xml, "application/xml");
});

// Robots.txt
app.MapGet("/robots.txt", () => Results.Content("""
                                                User-agent: *
                                                Allow: /
                                                Disallow: /dashboard
                                                Disallow: /judges
                                                Disallow: /courts
                                                Disallow: /cases
                                                Disallow: /detention
                                                Disallow: /removals
                                                Disallow: /settings
                                                Disallow: /alerts
                                                Disallow: /account

                                                Sitemap: https://interlimen.com/sitemap.xml
                                                """, "text/plain"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();