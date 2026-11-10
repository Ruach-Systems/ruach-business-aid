using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Dapper;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Ruach.BusinessAid.Api.Configuration;
using Ruach.BusinessAid.Api.Data;

namespace Ruach.BusinessAid.Api.Endpoints;

// Explicit opt-in, local database, loopback requests, fixed synthetic identity only.
public sealed class LocalTestSignIn
{
    public static bool Enabled(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("LocalTestAuth:Enabled")) return false;
        var sql = new SqlConnectionStringBuilder(configuration.GetConnectionString("Mashal"));
        return sql.DataSource.Equals(@".\SQLDEVELOPER2025", StringComparison.OrdinalIgnoreCase)
            && sql.InitialCatalog == "MashalDevelopment" && sql.IntegratedSecurity;
    }

    private static bool IsLocal(HttpContext ctx) => ctx.Request.Host.Host == "localhost"
        && ctx.Connection.RemoteIpAddress is {} ip && IPAddress.IsLoopback(ip);

    public static void Map(WebApplication app, AppOrigins origins)
    {
        if (!Enabled(app.Environment, app.Configuration)) return;
        app.MapGet("/api/auth/local-test", (HttpContext ctx, IAntiforgery csrf) =>
        {
            if (!IsLocal(ctx)) return Results.NotFound();
            ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'; form-action 'self' http://localhost:5173; frame-ancestors 'none'";
            var token = csrf.GetAndStoreTokens(ctx);
            return Results.Content($"""
                <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>Local test sign-in</title><h1>Local test sign-in</h1>
                <p>Sign in as Local Test Owner to use the local SQL-backed test business. This does not grant RUACH Admin access.</p>
                <form method="post"><input type="hidden" name="{HtmlEncoder.Default.Encode(token.FormFieldName)}" value="{HtmlEncoder.Default.Encode(token.RequestToken!)}">
                <button type="submit">Sign in as Local Test Owner</button></form></html>
                """, "text/html");
        });
        app.MapPost("/api/auth/local-test", async (HttpContext ctx, IAntiforgery csrf, SqlConnectionFactory connections, IdentityRepository identities) =>
        {
            if (!IsLocal(ctx)) return Results.NotFound();
            if (ctx.Request.Headers.Origin != $"{ctx.Request.Scheme}://{ctx.Request.Host}") return Results.StatusCode(403);
            await csrf.ValidateRequestAsync(ctx);
            await using var c = await connections.Open();
            var id = await c.QuerySingleOrDefaultAsync<Guid?>("SELECT Id FROM dbo.Users WHERE GoogleSubject='local-test-owner' AND Email='local-owner@example.invalid' AND EmailVerified=0");
            if (id is null || await identities.Find(id.Value) is not {} user || user.IsAdmin) return Results.StatusCode(403);
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.Value.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            return Results.Redirect(origins.RequirePwaOrigin(ctx.Request) + "/");
        }).WithMetadata(new LocalTestSignIn());
    }
}

