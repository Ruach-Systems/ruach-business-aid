using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
namespace Mashal.Tests;

[Trait("Category", "Integration")]
public class HttpTests
{
    private const string Origin = "https://businessaid.mashalsystems.com";
    private WebApplicationFactory<Program> Host() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Development");
        b.UseSetting("ConnectionStrings:Mashal", Environment.GetEnvironmentVariable("ConnectionStrings__Mashal"));
        b.UseSetting("Google:ClientId", "test-only-client"); b.UseSetting("Google:ClientSecret", "test-only-secret"); b.UseSetting("App:Origin", Origin);
        b.ConfigureServices(s => s.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; })
          .AddScheme<AuthenticationSchemeOptions, TestIdentityHandler>("Test", _ => { }));
    });
    [Fact]
    public async Task AnonymousEndpointsAreProtectedAndCorsIsExact()
    {
        using var host = Host(); using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Origin", Origin);
        var session = await client.GetAsync("/api/auth/session"); Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        Assert.Equal(Origin, session.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bootstrap")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/reports/summary?businessId=" + Guid.NewGuid() + "&from=2026-01-01&to=2026-01-31")).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        Assert.False((await client.GetAsync("/api/auth/session")).Headers.Contains("Access-Control-Allow-Origin"));
        var redirect = await client.GetAsync("/api/auth/google");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.StartsWith("https://accounts.google.com/", redirect.Headers.Location!.ToString());
    }
    [Fact]
    public async Task MutationsRequireCsrfAndSessionHydrationFindsExistingBusiness()
    {
        using var host = Host(); using var scope = host.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<IdentityRepository>().SignIn(Guid.NewGuid().ToString(), "HTTP Test", "test@example.invalid", null);
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", account.Uid.ToString()); client.DefaultRequestHeaders.Add("Origin", Origin);
        var business = Guid.NewGuid();
        var body = new { businessId = business, operations = new[] { new { id = Guid.NewGuid(), entityId = business, type = "createBusiness", payload = new { name = "HTTP Business", defaultLocation = "Main" } } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sync/push", body)).StatusCode);
        var csrf = await client.GetFromJsonAsync<Csrf>("/api/auth/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.Token);
        var push = await client.PostAsJsonAsync("/api/sync/push", body); Assert.Equal(HttpStatusCode.OK, push.StatusCode);
        var bootstrap = await client.GetFromJsonAsync<Mashal.BusinessAid.Shared.BootstrapResult>("/api/bootstrap");
        Assert.Equal(business, bootstrap!.Data.Business!.Id);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/sync/push", body)).StatusCode);
    }
    private sealed record Csrf(string Token);
}
// This scheme exists only in the test assembly. Production exposes no test-login endpoint.
public sealed class TestIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
 : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "Test");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
}
