using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ruach.BusinessAid.Api.Endpoints;
using Xunit;

namespace Ruach.BusinessAid.Tests;
public class LocalTestSignInTests
{
    [Theory]
    [InlineData("Development", true, @".\SQLDEVELOPER2025", "MashalDevelopment", true, true)]
    [InlineData("Production", true, @".\SQLDEVELOPER2025", "MashalDevelopment", true, false)]
    [InlineData("Staging", true, @".\SQLDEVELOPER2025", "MashalDevelopment", true, false)]
    [InlineData("Development", false, @".\SQLDEVELOPER2025", "MashalDevelopment", true, false)]
    [InlineData("Development", true, "remote.example", "MashalDevelopment", true, false)]
    [InlineData("Development", true, @".\SQLDEVELOPER2025", "Production", true, false)]
    [InlineData("Development", true, @".\SQLDEVELOPER2025", "MashalDevelopment", false, false)]
    public void OnlyExplicitLocalDevelopmentCanEnableSignIn(string environment, bool enabled, string server, string database, bool integrated, bool expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["LocalTestAuth:Enabled"] = enabled.ToString(),
            ["ConnectionStrings:Mashal"] = $"Server={server};Database={database};Integrated Security={integrated}"
        }).Build();
        Assert.Equal(expected, LocalTestSignIn.Enabled(new TestEnvironment { EnvironmentName=environment }, config));
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
