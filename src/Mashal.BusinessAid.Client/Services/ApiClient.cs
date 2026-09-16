using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Client.Services;

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public class ApiClient(HttpClient http, NavigationManager navigation)
{
    private string? token;
    public void SignIn() => navigation.NavigateTo(new Uri(http.BaseAddress!, "/api/auth/google").ToString(), true);
    public virtual async Task<T> Get<T>(string path) => (await Send<T>(HttpMethod.Get, path))!;
    public virtual async Task Post(string path, object body)
    {
        token ??= (await Get<Csrf>("/api/auth/antiforgery")).Token;
        try { await Send<JsonElement>(HttpMethod.Post, path, body); }
        catch (ApiException error) when (error.Code == "csrf") { token = null; throw; }
    }
    private async Task<T?> Send<T>(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.SetBrowserRequestCache(BrowserRequestCache.NoStore);
        if (body is not null) { request.Content = JsonContent.Create(body, options: Wire.Json); request.Headers.Add("X-CSRF-TOKEN", token); }
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync();
            string code = response.StatusCode == HttpStatusCode.Unauthorized ? "unauthorized" : "retry", message = "Could not contact the server. Your saved work remains on this device.";
            try { using var json = JsonDocument.Parse(problem); if (json.RootElement.TryGetProperty("code", out var c)) code = c.GetString()!; if (json.RootElement.TryGetProperty("title", out var t)) message = t.GetString()!; } catch (JsonException) { }
            throw new ApiException((int)response.StatusCode, code, message);
        }
        return response.StatusCode == HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<T>(Wire.Json);
    }
    public Task<T> Report<T>(string report, Guid business, string from, string to, string grouping = "day") => Get<T>($"/api/reports/{report}?businessId={business}&from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&grouping={grouping}");
    private sealed record Csrf(string Token);
}
