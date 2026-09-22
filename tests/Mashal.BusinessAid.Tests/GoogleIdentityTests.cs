using System.Text.Json;
using Mashal.BusinessAid.Api.Configuration;
using Mashal.BusinessAid.Api.Data;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mashal.BusinessAid.Tests;

public class GoogleIdentityTests
{
    [Theory]
    [InlineData("{\"email_verified\":true}", true)]
    [InlineData("{\"email_verified\":false}", false)]
    [InlineData("{}", false)]
    [InlineData("{\"email_verified\":null}", false)]
    [InlineData("{\"email_verified\":\"true\"}", false)]
    [InlineData("{\"verified_email\":true}", false)]
    public void GoogleV3VerificationControlsAllowlistedAdminAccess(string payload, bool expected)
    {
        Assert.Equal("https://www.googleapis.com/oauth2/v3/userinfo", new GoogleOptions().UserInformationEndpoint);
        using var user = JsonDocument.Parse(payload);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MashalAdmin:Emails:0"] = "admin@example.invalid"
        }).Build();
        var identities = new IdentityRepository(new SqlConnectionFactory("unused"), configuration);
        var verified = ApiServices.IsGoogleEmailVerified(user.RootElement);

        Assert.Equal(expected, identities.IsAdmin("admin@example.invalid", verified));
        Assert.False(identities.IsAdmin("other@example.invalid", verified));
    }
}
