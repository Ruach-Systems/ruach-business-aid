using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Dapper;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var origin = builder.Configuration["App:Origin"] ?? "https://businessaid.mashalsystems.com";
var connection = builder.Configuration.GetConnectionString("Mashal") ?? throw new InvalidOperationException("SQL connection configuration is required.");
var googleId = builder.Configuration["Google:ClientId"] ?? throw new InvalidOperationException("Google client configuration is required.");
var googleSecret = builder.Configuration["Google:ClientSecret"] ?? throw new InvalidOperationException("Google secret configuration is required.");
var secure = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddSingleton(new SqlConnectionFactory(connection));
builder.Services.AddScoped<IdentityRepository>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<ReportQueries>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origin).WithMethods("GET", "POST").WithHeaders("Content-Type", "X-CSRF-TOKEN").AllowCredentials()));
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "mashal.csrf"; o.Cookie.SecurePolicy = secure; o.Cookie.SameSite = SameSiteMode.Strict;
});
var protection = builder.Services.AddDataProtection().SetApplicationName("Mashal.BusinessAid")
 .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
if (!builder.Environment.IsDevelopment())
{
    var cert = builder.Configuration["DataProtection:CertificateBase64"] ?? throw new InvalidOperationException("Data protection certificate is required.");
    protection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(cert), builder.Configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet));
}
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(o =>
{
    o.Cookie.Name = "mashal.session"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = secure; o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromDays(14); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
}).AddGoogle(o =>
{
    o.ClientId = googleId; o.ClientSecret = googleSecret; o.CallbackPath = "/api/auth/google/callback"; o.SaveTokens = false;
    o.Events.OnCreatingTicket = async ctx =>
    {
        var subject = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing Google subject.");
        var account = await ctx.HttpContext.RequestServices.GetRequiredService<IdentityRepository>().SignIn(subject,
         ctx.Principal?.FindFirstValue(ClaimTypes.Name) ?? "", ctx.Principal?.FindFirstValue(ClaimTypes.Email) ?? "", null);
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, account.Uid.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, account.DisplayName));
        ctx.Principal = new ClaimsPrincipal(identity);
    };
    o.Events.OnRemoteFailure = ctx => { ctx.HandleResponse(); ctx.Response.Redirect(origin + "/sign-in?error=google"); return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception e)
    {
        var (status, code, message) = e switch
        {
            DomainException d => (d.Status, d.Code, d.Message),
            AntiforgeryValidationException => (400, "csrf", "Refresh your session and try again."),
            SqlException { Number: 2601 or 2627 } => (409, "conflict", "A record with this name or identifier already exists."),
            SqlException { Number: 547 } => (400, "validation", "A referenced record or value is invalid."),
            SqlException => (503, "retry", "The database is temporarily unavailable."),
            System.Text.Json.JsonException => (400, "validation", "Invalid request payload."),
            BadHttpRequestException => (400, "validation", "Invalid request."),
            _ => (500, "server_error", "The request could not be completed.")
        };
        app.Logger.LogError("Request failed with code {Code} ({Type}).", code, e.GetType().Name);
        if (ctx.Response.HasStarted) throw;
        ctx.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: message, extensions: new Dictionary<string, object?> { { "code", code } }).ExecuteAsync(ctx);
    }
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsPost(ctx.Request.Method))
    {
        if (ctx.Request.Headers.Origin.ToString() != origin) throw new DomainException("forbidden", "Origin is not allowed.", 403);
        if (ctx.User.Identity?.IsAuthenticated != true) throw new DomainException("unauthorized", "Sign in again to synchronize.", 401);
        await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);
    }
    await next();
});
Guid UserId(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (SqlConnectionFactory f) =>
{
    await using var c = await f.Open();
    await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions; SELECT TOP(0) Id FROM dbo.Businesses;");
    return Results.Ok(new { status = "ready" });
});
app.MapGet("/api/auth/google", () => Results.Challenge(new AuthenticationProperties { RedirectUri = origin + "/", IsPersistent = true }, [GoogleDefaults.AuthenticationScheme]));
app.MapGet("/api/auth/session", async (HttpContext ctx, IdentityRepository repo) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var user = await repo.Find(UserId(ctx)); return user is null ? Results.Unauthorized() : Results.Ok(user);
});
app.MapGet("/api/auth/antiforgery", (HttpContext ctx, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(ctx).RequestToken })).RequireAuthorization();
app.MapPost("/api/auth/logout", async (HttpContext ctx) => { await ctx.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
app.MapGet("/api/bootstrap", async (HttpContext ctx, IBusinessService service) => await service.Bootstrap(UserId(ctx))).RequireAuthorization();
app.MapPost("/api/sync/push", async (HttpContext ctx, PushRequest request, IBusinessService service) =>
{
    await service.Push(UserId(ctx), request); return Results.Ok(new { accepted = request.Operations.Select(x => x.Id) });
}).RequireAuthorization();
app.MapGet("/api/sync/pull", async (HttpContext ctx, Guid businessId, long cursor, IBusinessService service) => await service.Pull(UserId(ctx), businessId, cursor)).RequireAuthorization();
app.MapGet("/api/reports/{report}", async (HttpContext ctx, string report, Guid businessId, string from, string to, string? grouping, ReportQueries reports) =>
 await reports.Query(UserId(ctx), businessId, report, from, to, grouping ?? "day")).RequireAuthorization();
app.Run();
public partial class Program;
