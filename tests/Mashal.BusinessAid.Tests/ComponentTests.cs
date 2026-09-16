using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mashal.BusinessAid.Client.Services;
using Mashal.BusinessAid.Client.Pages;
using Mashal.BusinessAid.Client.Components;
using Xunit;
namespace Mashal.BusinessAid.Tests;

public class ComponentTests
{
    [Theory]
    [InlineData("2026-01-01", "Today")]
    [InlineData("2025-12-31", "Yesterday")]
    [InlineData("2025-09-15", "Sep 15, 2025")]
    [InlineData("2026-01-02", "Jan 2, 2026")]
    public void SaleDatesAreReadable(string value, string expected) =>
        Assert.Equal(expected, Mashal.BusinessAid.Shared.Calculations.FriendlyDate(value, new DateOnly(2026, 1, 1)));

    [Fact]
    public void MoreIconUsesVisibleFilledDots()
    {
        using var context = Context();
        var icon = context.Render<Icon>(p => p.Add(x => x.Name, "more"));
        Assert.Equal(3, icon.FindAll("circle[fill='currentColor'][r='2']").Count);
    }

    [Fact]
    public void MoneyInputKeepsTypingThenFormatsAndRoundsOnBlur()
    {
        using var context = Context();
        decimal saved = 0;
        var input = context.Render<MoneyInput>(p => p.Add(x => x.Value, 35m).Add(x => x.ValueChanged, x => saved = x));
        Assert.Equal("35.00", input.Find("input").GetAttribute("value"));
        input.Find("input").Input("12.345");
        Assert.Equal(12.345m, saved);
        input.Find("input").Blur();
        Assert.Equal(12.35m, saved);
        Assert.Equal("12.35", input.Find("input").GetAttribute("value"));
        input.Find("input").Input("0");
        input.Find("input").Blur();
        Assert.Equal("0.00", input.Find("input").GetAttribute("value"));
    }
    [Theory]
    [InlineData("g", 33.33, "grams")]
    [InlineData("g", 1, "gram")]
    [InlineData("pc", 0, "pieces")]
    [InlineData("pc", 1, "piece")]
    [InlineData("ml", 200, "milliliters")]
    public void UnitNamesAreReadable(string unit, decimal quantity, string expected) =>
        Assert.Equal(expected, Mashal.BusinessAid.Shared.Calculations.UnitName(unit, quantity));
    private sealed class TestEnvironment : IWebAssemblyHostEnvironment
    {
        public string Environment => "Development"; public string ApplicationName => "Mashal.BusinessAid.Client"; public string BaseAddress => "http://localhost:5173/";
    }
    private static BunitContext Context()
    {
        var context = new BunitContext(); context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IWebAssemblyHostEnvironment>(new TestEnvironment());
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(new HttpClient { BaseAddress = new Uri("http://localhost:5080") });
        context.Services.AddScoped<ApiClient>(); context.Services.AddScoped<OfflineStorage>(); context.Services.AddScoped<SyncService>(); context.Services.AddScoped<BusinessState>();
        return context;
    }
    [Fact]
    public void SignInRetainsBrandAndDevelopmentLocalOption()
    {
        using var context = Context(); var page = context.Render<SignIn>();
        Assert.Contains("Continue with Google", page.Markup); Assert.Contains("Continue locally", page.Markup);
        Assert.DoesNotContain("Reset sign-in state", page.Markup);
        Assert.Contains("mashal-wordmark-reversed", page.Find("img").GetAttribute("src"));
    }
    [Fact]
    public void ProductDefaultsToNotTrackedAndShowsPreparedSecond()
    {
        using var context = Context(); var page = context.Render<ProductEditor>();
        var modes = page.FindAll("[role=radio]");
        Assert.Equal(2, modes.Count); Assert.Contains("Not tracked", modes[0].TextContent); Assert.Equal("true", modes[0].GetAttribute("aria-checked"));
        modes[1].Click(); Assert.Contains("Batch ingredients", page.Markup); Assert.Contains("How many finished items", page.Markup); Assert.DoesNotContain("Made when sold", page.Markup);
        var yield = page.Find("#recipe-batch-yield"); yield.Input("10"); Assert.Equal("10", yield.GetAttribute("value"));
        Assert.Contains("Create your first ingredient", page.Markup);
        Assert.NotNull(page.Find("#quick-item-name"));
    }
    [Fact]
    public void OnboardingProvidesAccountExitWithoutFakeProgress()
    {
        using var context = Context(); var page = context.Render<Onboarding>();
        Assert.Contains("Cancel and use a different account", page.Markup); Assert.Empty(page.FindAll("[role=progressbar]"));
        Assert.True(page.Find("#business-name").HasAttribute("required"));
    }
    [Fact]
    public async Task SettingsOffersConfirmedResetOnlyInDevelopmentLocalMode()
    {
        using var context = Context();
        var signedOut = context.Render<Settings>();
        Assert.DoesNotContain("Reset local data", signedOut.Markup);

        var store = context.Services.GetRequiredService<BusinessState>();
        await store.ContinueLocally();
        var page = context.Render<Settings>();
        Assert.Contains("Reset local data", page.Markup);
        page.Find("button.danger.block").Click();
        Assert.Contains("Delete all local demo data?", page.Markup);
        Assert.Contains("Delete and start over", page.Markup);
    }
    [Fact]
    public void ErrorBannerUsesAlertSemanticsAndReportsUseOnlineNotice()
    {
        using var context = Context(); var error = context.Render<ErrorBanner>(p => p.Add(x => x.Message, "Record changed"));
        Assert.Equal("Record changed", error.Find("[role=alert]").TextContent);
        var totals = context.Render<OnlineTotals>();
        Assert.Equal("Summary", totals.Find("h2").TextContent);
        Assert.Contains("Weekly and monthly sales, expenses, and estimated profit.", totals.Markup);
    }
}
