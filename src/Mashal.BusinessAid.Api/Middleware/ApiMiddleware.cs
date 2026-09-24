using Mashal.BusinessAid.Shared;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Mashal.BusinessAid.Api.Middleware;
public static class ApiMiddleware
{
    public static void UseApiMiddleware(this WebApplication app, string origin)
    {
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            try
            {
                await next();
            }
            catch (Exception e)
            {
                var(status, code, message) = e switch
                {
                    DomainException d => (d.Status, d.Code, d.Message),
                    AntiforgeryValidationException => (400, "csrf", "Refresh your session and try again."),
                    SqlException { Number: 2601 or 2627 } => (409, "conflict", "A record with this name or identifier already exists."),
                    SqlException { Number: 547 } => (400, "validation", "A referenced record or value is invalid."),
                    SqlException => (503, "retry", "The database is temporarily unavailable."),
                    System.Text.Json.JsonException => (400, "validation", "Invalid request payload."),
                    BadHttpRequestException => (400, "validation", "Invalid request."),
                    _ => (500, "server_error", "The request could not be completed.")};
                app.Logger.LogError(e, "Request failed with code {Code} ({Type}).", code, e.GetType().Name);
                if (ctx.Response.HasStarted)
                    throw;
                ctx.Response.StatusCode = status;
                await Results.Problem(statusCode: status, title: message, extensions: new Dictionary<string, object?> { { "code", code } }).ExecuteAsync(ctx);
            }
        });
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (ctx, next) =>
        {
            if (HttpMethods.IsPost(ctx.Request.Method))
            {
                if (ctx.Request.Headers.Origin.ToString() != origin)
                    throw new DomainException("forbidden", "Origin is not allowed.", 403);
                if (ctx.User.Identity?.IsAuthenticated != true)
                    throw new DomainException("unauthorized", "Sign in again to synchronize.", 401);
                await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);
            }

            await next();
        });
    }
}
