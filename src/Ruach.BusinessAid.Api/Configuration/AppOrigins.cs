using Ruach.BusinessAid.Shared;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Ruach.BusinessAid.Api.Configuration;

public sealed class AppOrigins
{
    private readonly Dictionary<string, string> pairs = new(StringComparer.Ordinal);
    private readonly string legacyOrigin;

    public AppOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        legacyOrigin = Validate(configuration["App:Origin"] ?? "https://businessaid.mashalsystems.com", environment);
        foreach (var pair in configuration.GetSection("App:OriginPairs").GetChildren())
        {
            var api = Validate(pair["ApiOrigin"], environment);
            var pwa = Validate(pair["PwaOrigin"], environment);
            if (!pairs.TryAdd(api, pwa))
                throw new InvalidOperationException("Each API origin must have exactly one PWA origin.");
        }
    }

    public string? FindPwaOrigin(HttpRequest request)
    {
        if (pairs.Count == 0)
            return legacyOrigin;
        return pairs.GetValueOrDefault($"{request.Scheme}://{request.Host}");
    }

    public string RequirePwaOrigin(HttpRequest request)
        => FindPwaOrigin(request) ?? throw new DomainException("forbidden", "API origin is not configured.", 403);

    private static string Validate(string? value, IHostEnvironment environment)
    {
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != "/" || uri.Host.Contains('*')
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || value != uri.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("App origins must be canonical HTTPS origins without paths or trailing slashes; HTTP loopback is allowed only in Development.");
        return value;
    }
}

public sealed class AppCorsPolicyProvider(AppOrigins origins) : ICorsPolicyProvider
{
    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var builder = new CorsPolicyBuilder().WithMethods("GET", "POST")
            .WithHeaders("Content-Type", "X-CSRF-TOKEN").AllowCredentials();
        if (origins.FindPwaOrigin(context.Request) is { } origin)
            builder.WithOrigins(origin);
        return Task.FromResult<CorsPolicy?>(builder.Build());
    }
}
