using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Ruach.BusinessAid.Tests;
public partial class BrowserTests
{
    [BrowserFact]
    public async Task DiscardConfirmsCountCancelsAndProtectsChangedPendingQueue()
    {
        await using var host=await Preview.Start();
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
        await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=390,Height=844}});
        var owner=new AppUser(Guid.NewGuid(),"Sync tester","sync@example.invalid",null){PhoneNumber="+639171234567"};
        var data=new AppData{Business=new(){Id=Guid.NewGuid(),OwnerUid=owner.Uid,Name="Sync shop",DefaultLocation="Main"}};
        await MockApi(context,owner,data);
        var page=await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button,new(){Name="Open Sync shop",Exact=true}).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Today", Exact = true})).ToBeVisibleAsync();
        var key=$"{owner.Uid}:{data.Business.Id}";
        const string seed="""
        async key => {
          const s=JSON.parse(await mashalStorage.read('accounts',key));
          const id=crypto.randomUUID();
          s.outbox.push({id:crypto.randomUUID(),entityId:id,type:'saveExpense',payload:{id,description:'Pending fare',category:'Transportation',amountCentavos:100,expenseDate:'2026-10-01'},projections:[],prepared:true,dependsOnPending:false,error:'conflict'});
          s.revision++;
          await mashalStorage.write('accounts',key,JSON.stringify(s));
        }
        """;
        await page.EvaluateAsync(seed,key);
        await page.ReloadAsync();
        var use=page.GetByRole(AriaRole.Button,new(){Name="Use server records",Exact=true});
        await Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button,new(){Name="OK",Exact=true}).ClickAsync();
        await use.ClickAsync();
        var dialog=page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToContainTextAsync("all 1 pending change for this business");
        await dialog.GetByRole(AriaRole.Button,new(){Name="Cancel",Exact=true}).ClickAsync();
        Assert.Equal(1,(await ReadState(page,owner.Uid)).GetProperty("outbox").GetArrayLength());
        await use.ClickAsync();
        // Another tab writes while the confirmation remains open.
        var second=await context.NewPageAsync();
        await second.GotoAsync(host.Url);
        await second.EvaluateAsync(seed,key);
        await dialog.GetByRole(AriaRole.Button,new(){Name="Discard pending changes",Exact=true}).ClickAsync();
        await Expect(dialog).ToContainTextAsync("pending changes have changed");
        Assert.Equal(2,(await ReadState(page,owner.Uid)).GetProperty("outbox").GetArrayLength());
        await dialog.GetByRole(AriaRole.Button,new(){Name="OK",Exact=true}).ClickAsync();
        await use.ClickAsync();
        await Expect(dialog).ToContainTextAsync("all 2 pending changes");
        await dialog.GetByRole(AriaRole.Button,new(){Name="Discard pending changes",Exact=true}).ClickAsync();
        await Expect(use).ToHaveCountAsync(0);
        Assert.Equal(0,(await ReadState(page,owner.Uid)).GetProperty("outbox").GetArrayLength());
    }
}
