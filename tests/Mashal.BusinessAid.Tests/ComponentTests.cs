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
    public void ItemEditorKeepsPriceAndCostTogetherWithoutProductionOptions()
    {
        using var context = Context(); var page = context.Render<InventoryEditor>();
        Assert.NotNull(page.Find("#item-name"));
        Assert.NotNull(page.Find("#item-price"));
        Assert.NotNull(page.Find("#item-cost"));
        Assert.Contains("Profit per item", page.Markup);
        Assert.Contains("Markup", page.Markup);
        Assert.Equal("-", page.Find(".cost-preview div:nth-child(2) strong").TextContent);
        Assert.DoesNotContain("Not applicable", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Margin", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipe", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("production", page.Markup, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void ReportsOfferOnlySimpleBusinessPeriods()
    {
        using var context = Context(); var page = context.Render<Reports>();
        var options = page.FindAll("#report-period option").Select(x => x.TextContent).ToArray();
        Assert.Equal(new[] { "Today", "This week", "This month" }, options);
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
        Assert.Contains("Weekly and monthly sales, expenses, and net profit.", totals.Markup);
    }
}
