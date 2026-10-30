using System.Net;
using Ruach.BusinessAid.Client.Services;
using Xunit;
namespace Ruach.BusinessAid.Tests;
public class NetworkMessageTests
{
    private sealed class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(response());
    }
    [Theory]
    [InlineData(503, "temporarily unavailable")]
    [InlineData(401, "Sign in again")]
    public async Task StatusFailuresGiveActionableMessages(int status,string expected)
    {
        using var http=new HttpClient(new Handler(()=>new HttpResponseMessage((HttpStatusCode)status){Content=new StringContent("proxy error")})){BaseAddress=new Uri("https://example.invalid")};
        var error=await Assert.ThrowsAsync<ApiException>(()=>new ApiClient(http,null!).Get<object>("/test"));
        Assert.Contains(expected,error.Message);
        Assert.Equal(status,error.Status);
    }
    [Fact]
    public async Task TransportFailurePreservesHttpExceptionTypeAndGivesConnectionAdvice()
    {
        using var http=new HttpClient(new Handler(()=>throw new HttpRequestException("Failed to fetch"))){BaseAddress=new Uri("https://example.invalid")};
        var error=await Assert.ThrowsAsync<HttpRequestException>(()=>new ApiClient(http,null!).Get<object>("/test"));
        Assert.Contains("Check your internet connection",error.Message);
        Assert.Contains("Pending changes remain saved",error.Message);
    }
}
