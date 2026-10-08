using System.Security.Claims;
using Dapper;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Mashal.BusinessAid.Api.Configuration;

namespace Mashal.BusinessAid.Api.Endpoints;
public static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app, AppOrigins origins)
    {
        Guid UserId(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health/ready", async (SqlConnectionFactory f) =>
        {
            await using var c = await f.Open();
            await c.ExecuteAsync("SELECT TOP(0) Id FROM dbo.Businesses; SELECT TOP(0) PhoneNumber,EmailVerified FROM dbo.Users; SELECT TOP(0) SellingPriceCentavos,UnitCostCentavos FROM dbo.Items; SELECT TOP(0) [Cursor] FROM dbo.ItemSyncChanges; SELECT TOP(0) Id FROM dbo.BusinessRequests; SELECT TOP(0) Id FROM dbo.AdminAudit;");
            return Results.Ok(new { status = "ready" });
        });
        app.MapGet("/api/auth/google", (HttpContext ctx) => Results.Challenge(new AuthenticationProperties { RedirectUri = origins.RequirePwaOrigin(ctx.Request) + "/", IsPersistent = true }, [GoogleDefaults.AuthenticationScheme]));
        app.MapGet("/api/auth/session", async (HttpContext ctx, IdentityRepository repo) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            var user = await repo.Find(UserId(ctx));
            return user is null ? Results.Unauthorized() : Results.Ok(user);
        });
        app.MapGet("/api/auth/antiforgery", (HttpContext ctx, IAntiforgery csrf) => Results.Ok(new { token = csrf.GetAndStoreTokens(ctx).RequestToken })).RequireAuthorization();
        app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapGet("/api/bootstrap", async (HttpContext ctx, Guid? businessId, IBusinessService service) => await service.Bootstrap(UserId(ctx), businessId)).RequireAuthorization();
        app.MapGet("/api/account", async (HttpContext ctx, AccountService service) => await service.Overview(UserId(ctx))).RequireAuthorization();
        app.MapPost("/api/account/phone", async (HttpContext ctx, PhoneInput input, AccountService service) =>
        {
            await service.SavePhone(UserId(ctx), input);
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapPost("/api/account/requests", async (HttpContext ctx, BusinessRequestInput input, AccountService service) =>
        {
            await service.Request(UserId(ctx), input);
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapGet("/api/admin", async (HttpContext ctx, AccountService service) => await service.Admin(UserId(ctx))).RequireAuthorization();
        app.MapPost("/api/admin/requests/{id:guid}/decision", async (HttpContext ctx, Guid id, DecisionInput input, AccountService service) =>
        {
            await service.Decide(UserId(ctx), id, input);
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapPost("/api/admin/phone-transfer", async (HttpContext ctx, PhoneTransferInput input, AccountService service) =>
        {
            await service.Transfer(UserId(ctx), input);
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapPost("/api/sync/push", async (HttpContext ctx, PushRequest request, IBusinessService service) =>
        {
            await service.Push(UserId(ctx), request);
            return Results.Ok(new { accepted = request.Operations.Select(x => x.Id) });
        }).RequireAuthorization();
        app.MapGet("/api/sync/pull", async (HttpContext ctx, Guid businessId, long cursor, int? modelVersion, IBusinessService service) =>
        {
            if (modelVersion != DataModel.CurrentVersion)
                throw new DomainException("client_upgrade_required", "Refresh Business Aid to use the simplified Items update.", 426);
            return await service.Pull(UserId(ctx), businessId, cursor);
        }).RequireAuthorization();
        app.MapGet("/api/reports/{report}", async (HttpContext ctx, string report, Guid businessId, string from, string to, string? grouping, ReportQueries reports) => await reports.Query(UserId(ctx), businessId, report, from, to, grouping ?? "day")).RequireAuthorization();
    }
}
