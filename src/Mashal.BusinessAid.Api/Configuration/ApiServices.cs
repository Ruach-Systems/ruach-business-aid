using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;

namespace Mashal.BusinessAid.Api.Configuration;
public static class ApiServices
{
    public static bool IsGoogleEmailVerified(System.Text.Json.JsonElement user)
        => user.TryGetProperty("email_verified", out var verified) && verified.ValueKind == System.Text.Json.JsonValueKind.True;

    public static void AddApiServices(this WebApplicationBuilder builder, string origin)
    {
        var connection = builder.Configuration.GetConnectionString("Mashal") ?? throw new InvalidOperationException("SQL connection configuration is required.");
        var googleId = builder.Configuration["Google:ClientId"] ?? throw new InvalidOperationException("Google client configuration is required.");
        var googleSecret = builder.Configuration["Google:ClientSecret"] ?? throw new InvalidOperationException("Google secret configuration is required.");
        var secure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        builder.Services.AddSingleton(new SqlConnectionFactory(connection));
        builder.Services.AddScoped<IdentityRepository>();
        builder.Services.AddScoped<AccountService>();
        builder.Services.AddScoped<IBusinessService, BusinessService>();
        builder.Services.AddScoped<ReportQueries>();
        builder.Services.AddProblemDetails();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origin).WithMethods("GET", "POST").WithHeaders("Content-Type", "X-CSRF-TOKEN").AllowCredentials()));
        builder.Services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN";
            o.Cookie.Name = "mashal.csrf";
            o.Cookie.SecurePolicy = secure;
            o.Cookie.SameSite = SameSiteMode.Strict;
        });
        var protection = builder.Services.AddDataProtection().SetApplicationName("Mashal.BusinessAid").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
        if (!builder.Environment.IsDevelopment())
        {
            var cert = builder.Configuration["DataProtection:CertificateBase64"] ?? throw new InvalidOperationException("Data protection certificate is required.");
            // Shared IIS application pools don't necessarily load a user profile.
            // Use the machine key store on Windows so PFX import doesn't depend on one.
            var storage = OperatingSystem.IsWindows() ? X509KeyStorageFlags.MachineKeySet : X509KeyStorageFlags.EphemeralKeySet;
            protection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(cert), builder.Configuration["DataProtection:CertificatePassword"], storage));
        }

        builder.Services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        }).AddCookie(o =>
        {
            o.Cookie.Name = "mashal.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = secure;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromDays(14);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = c =>
            {
                c.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = c =>
            {
                c.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        }).AddGoogle(o =>
        {
            o.ClientId = googleId;
            o.ClientSecret = googleSecret;
            o.CallbackPath = "/api/auth/google/callback";
            o.SaveTokens = false;
            o.Events.OnCreatingTicket = async ctx =>
            {
                var subject = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing Google subject.");
                var account = await ctx.HttpContext.RequestServices.GetRequiredService<IdentityRepository>().SignIn(subject, ctx.Principal?.FindFirstValue(ClaimTypes.Name) ?? "", ctx.Principal?.FindFirstValue(ClaimTypes.Email) ?? "", null, IsGoogleEmailVerified(ctx.User));
                var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, account.Uid.ToString()));
                identity.AddClaim(new Claim(ClaimTypes.Name, account.DisplayName));
                ctx.Principal = new ClaimsPrincipal(identity);
            };
            o.Events.OnRemoteFailure = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.Redirect(origin + "/sign-in?error=google");
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization();
    }
}
