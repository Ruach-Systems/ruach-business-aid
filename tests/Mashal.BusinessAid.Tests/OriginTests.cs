using System.Net;
using System.Net.Http.Json;
using Mashal.BusinessAid.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mashal.Tests;

public class OriginTests
{
    private const string OldApi = "https://api-businessaid.mashalsystems.com";
    private const string OldPwa = "https://businessaid.mashalsystems.com";
    private const string NewApi = "https://api.businessaid.ruachsystems.dev";
    private const string NewPwa = "https://businessaid.ruachsystems.dev";

    private static WebApplicationFactory<Program> Host() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Development");
        b.UseSetting("ConnectionStrings:Mashal", "Server=unused;Database=unused");
        b.UseSetting("Google:ClientId", "test-only-client");
        b.UseSetting("Google:ClientSecret", "test-only-secret");
        b.UseSetting("App:OriginPairs:0:ApiOrigin", OldApi);
        b.UseSetting("App:OriginPairs:0:PwaOrigin", OldPwa);
        b.UseSetting("App:OriginPairs:1:ApiOrigin", NewApi);
        b.UseSetting("App:OriginPairs:1:PwaOrigin", NewPwa);
        b.ConfigureServices(s => s.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = "Test";
            o.DefaultChallengeScheme = "Test";
        }).AddScheme<AuthenticationSchemeOptions, TestIdentityHandler>("Test", _ => { }));
    });

    [Theory]
    [InlineData(OldApi, OldPwa)]
    [InlineData(NewApi, NewPwa)]
    public async Task CorsAndOAuthStayWithinTheRequestedApiPair(string api, string pwa)
    {
        using var host = Host();
        using var client = host.CreateClient(new() { BaseAddress = new Uri(api), AllowAutoRedirect = false });
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/logout");
        preflight.Headers.Add("Origin", pwa);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "X-CSRF-TOKEN,Content-Type");
        var response = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(pwa, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        var challenge = await client.GetAsync("/api/auth/google?returnUrl=https://untrusted.example");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        Assert.Equal(api + "/api/auth/google/callback", query["redirect_uri"].ToString());
        var options = host.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get(GoogleDefaults.AuthenticationScheme);
        var state = options.StateDataFormat.Unprotect(query["state"].ToString());
        Assert.Equal(pwa + "/", state!.RedirectUri);

        var failed = await client.GetAsync("/api/auth/google/callback?error=access_denied");
        Assert.Equal(pwa + "/sign-in?error=google", failed.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(OldApi, NewPwa)]
    [InlineData(NewApi, OldPwa)]
    [InlineData(NewApi, "https://untrusted.example")]
    [InlineData(NewApi, "null")]
    [InlineData("https://unknown.example", NewPwa)]
    public async Task ForeignAndCrossPairedOriginsCannotReadOrMutate(string api, string origin)
    {
        using var host = Host();
        using var client = host.CreateClient(new() { BaseAddress = new Uri(api), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("Origin", origin);
        var get = await client.GetAsync("/health/live");
        Assert.False(get.Headers.Contains("Access-Control-Allow-Origin"));
        var post = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.False(post.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UnknownApiCannotInitiateOAuthAndMissingOriginCannotMutate()
    {
        using var host = Host();
        using var unknown = host.CreateClient(new() { BaseAddress = new Uri("https://unknown.example"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Forbidden, (await unknown.GetAsync("/api/auth/google")).StatusCode);
        using var known = host.CreateClient(new() { BaseAddress = new Uri(NewApi), AllowAutoRedirect = false });
        known.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await known.PostAsync("/api/auth/logout", null)).StatusCode);
        known.DefaultRequestHeaders.Add("Origin", NewPwa);
        Assert.Equal(HttpStatusCode.BadRequest, (await known.PostAsync("/api/auth/logout", null)).StatusCode);
        known.DefaultRequestHeaders.Remove("X-Test-User");
        Assert.Equal(HttpStatusCode.Unauthorized, (await known.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Theory]
    [InlineData(OldApi, OldPwa)]
    [InlineData(NewApi, NewPwa)]
    public async Task MatchingPairsStillRequireValidAntiforgeryTokens(string api, string pwa)
    {
        using var host = Host();
        using var client = host.CreateClient(new() { BaseAddress = new Uri(api), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("Origin", pwa);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        var csrf = await client.GetFromJsonAsync<Csrf>("/api/auth/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public void DuplicateApiMappingsAreRejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:OriginPairs:0:ApiOrigin"] = NewApi,
            ["App:OriginPairs:0:PwaOrigin"] = NewPwa,
            ["App:OriginPairs:1:ApiOrigin"] = NewApi,
            ["App:OriginPairs:1:PwaOrigin"] = OldPwa
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new AppOrigins(config, new HostEnvironment { EnvironmentName = Environments.Production }));
    }

    [Theory]
    [InlineData("https://site.example/")]
    [InlineData("https://site.example/path")]
    [InlineData("https://site.example?query=1")]
    [InlineData("https://user@site.example")]
    [InlineData("https://*.example")]
    [InlineData("http://site.example")]
    [InlineData("http://localhost:5173")]
    public void ProductionConfigurationRejectsNonCanonicalAndInsecureOrigins(string origin)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:Origin"] = origin }).Build();
        Assert.Throws<InvalidOperationException>(() => new AppOrigins(config, new HostEnvironment { EnvironmentName = Environments.Production }));
    }

    [Fact]
    public void LegacySingleOriginAndDevelopmentLoopbackRemainCompatible()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:Origin"] = "http://localhost:5173" }).Build();
        var origins = new AppOrigins(config, new HostEnvironment { EnvironmentName = Environments.Development });
        Assert.Equal("http://localhost:5173", origins.RequirePwaOrigin(new DefaultHttpContext().Request));
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed record Csrf(string Token);
}
