using System.Text.Json;
using Dapper;
using Ruach.BusinessAid.Api.Data;
using Ruach.BusinessAid.Shared;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Ruach.Tests;

internal static class AccountFixture
{
    public static IConfiguration Configuration => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["MashalAdmin:Emails:0"]="admin@example.invalid" }).Build();
    public static string Phone() => "+639" + Random.Shared.NextInt64(100000000,999999999);
    public static async Task<Guid> Provision(SqlConnectionFactory connections, Guid user)
    {
        var identities=new IdentityRepository(connections,Configuration);
        var admin=await identities.SignIn(Guid.NewGuid().ToString(),"Admin","admin@example.invalid",null,true);
        var service=new AccountService(connections,identities);
        await service.SavePhone(user,new(Phone()));
        var id=Guid.NewGuid(); await service.Request(user,new(id,"Test business","Main"));
        await service.Decide(admin.Uid,id,new(true,null)); return id;
    }
}
[Trait("Category","Integration")]
public class AccountSqlTests
{
    private readonly SqlConnectionFactory connections=new(Environment.GetEnvironmentVariable("ConnectionStrings__Mashal")!);
    [Fact]
    public async Task OneOwnerHasIsolatedBusinessesAndCannotSelfProvision()
    {
        var identities=new IdentityRepository(connections,AccountFixture.Configuration);
        var owner=await identities.SignIn(Guid.NewGuid().ToString(),"Owner","owner@example.invalid",null);
        var a=await AccountFixture.Provision(connections,owner.Uid); var b=await AccountFixture.Provision(connections,owner.Uid);
        var service=new BusinessService(connections,identities); var id=Guid.NewGuid();
        var op=new Operation(Guid.NewGuid(),id,"saveExpense",null,JsonSerializer.SerializeToElement(new Expense {Description="A only",Category="Transport",AmountCentavos=100,ExpenseDate=Calculations.Today().ToString("yyyy-MM-dd")},Wire.Json));
        await service.Push(owner.Uid,new(DataModel.CurrentVersion,a,[op]));
        Assert.Single((await service.Bootstrap(owner.Uid,a)).Data.Expenses);
        Assert.Empty((await service.Bootstrap(owner.Uid,b)).Data.Expenses);
        Assert.DoesNotContain((await service.Pull(owner.Uid,b,0)).Changes,x=>x.CollectionName=="expenses");
        Assert.Null((await service.Bootstrap(owner.Uid)).Data.Business);
        await Assert.ThrowsAsync<DomainException>(()=>service.Push(owner.Uid,new(DataModel.CurrentVersion,a,[op with { Id=Guid.NewGuid(),Type="createBusiness" }])));
        var outsider=await identities.SignIn(Guid.NewGuid().ToString(),"Other","other@example.invalid",null);
        await new AccountService(connections,identities).SavePhone(outsider.Uid,new(AccountFixture.Phone()));
        await Assert.ThrowsAsync<DomainException>(()=>service.Bootstrap(outsider.Uid,a));
        await Assert.ThrowsAsync<DomainException>(()=>new ReportQueries(connections).Query(outsider.Uid,a,"summary","2026-01-01","2026-12-31"));
    }
    [Fact]
    public async Task PhoneClaimsAreUniqueIncludingRacesAndTransferBlocksPreviousOwner()
    {
        var identities=new IdentityRepository(connections,AccountFixture.Configuration); var service=new AccountService(connections,identities);
        var a=await identities.SignIn(Guid.NewGuid().ToString(),"A","a@example.invalid",null);
        var b=await identities.SignIn(Guid.NewGuid().ToString(),"B","b@example.invalid",null);
        var phone=AccountFixture.Phone();
        var results=await Task.WhenAll(new[]{a,b}.Select(async u=> {try {await service.SavePhone(u.Uid,new(phone));return true;}catch(DomainException e){Assert.Equal("phone_in_use",e.Code);return false;}}));
        Assert.Single(results,x=>x);
        var from=results[0]?a:b; var to=results[0]?b:a;
        var business=await AccountFixture.Provision(connections,from.Uid);
        phone=(await identities.Find(from.Uid))!.PhoneNumber!;
        var admin=await identities.SignIn(Guid.NewGuid().ToString(),"Admin","admin@example.invalid",null,true);
        var recipientPhone=AccountFixture.Phone();
        await service.SavePhone(to.Uid,new(recipientPhone));
        var transfer=new PhoneTransferInput(from.Uid,to.Uid,phone,"Offline identity check reference",true,recipientPhone);
        await Assert.ThrowsAsync<DomainException>(()=>service.Transfer(from.Uid,transfer));
        await Assert.ThrowsAsync<DomainException>(()=>service.Transfer(admin.Uid,transfer with {IdentityChecked=false}));
        var stale=await Assert.ThrowsAsync<DomainException>(()=>service.Transfer(admin.Uid,transfer with {ExpectedRecipientPhone=null}));
        Assert.Equal("conflict",stale.Code);
        Assert.Equal(phone,(await identities.Find(from.Uid))!.PhoneNumber);
        Assert.Equal(recipientPhone,(await identities.Find(to.Uid))!.PhoneNumber);
        await service.Transfer(admin.Uid,transfer);
        Assert.Null((await identities.Find(from.Uid))!.PhoneNumber);
        Assert.Equal(phone,(await identities.Find(to.Uid))!.PhoneNumber);
        Assert.Single((await service.Overview(from.Uid)).Businesses);
        var denied=await Assert.ThrowsAsync<DomainException>(()=>new BusinessService(connections,identities).Bootstrap(from.Uid,business));
        Assert.Equal("phone_required",denied.Code);
        await Assert.ThrowsAsync<DomainException>(()=>service.SavePhone(from.Uid,new(phone)));
        await service.SavePhone(from.Uid,new(recipientPhone));
        Assert.Equal(business,(await new BusinessService(connections,identities).Bootstrap(from.Uid,business)).Data.Business!.Id);
        await using var c=await connections.Open();
        Assert.Equal(1,await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AdminAudit WHERE SubjectUid=@Uid AND Action='PhoneTransfer'",from));
        Assert.Equal(recipientPhone,await c.ExecuteScalarAsync<string>("SELECT PreviousRecipientPhone FROM dbo.AdminAudit WHERE SubjectUid=@Uid AND Action='PhoneTransfer'",from));
    }
    [Fact]
    public async Task PendingRequestsDecisionsAndAdminAllowlistAreEnforced()
    {
        var identities=new IdentityRepository(connections,AccountFixture.Configuration); var service=new AccountService(connections,identities);
        var owner=await identities.SignIn(Guid.NewGuid().ToString(),"Owner","owner@example.invalid",null);
        var unverified=await identities.SignIn(Guid.NewGuid().ToString(),"Unverified","admin@example.invalid",null,false);
        var admin=await identities.SignIn(Guid.NewGuid().ToString(),"Admin","admin@example.invalid",null,true);
        await Assert.ThrowsAsync<DomainException>(()=>service.Admin(owner.Uid));
        await Assert.ThrowsAsync<DomainException>(()=>service.Admin(unverified.Uid));
        var id=Guid.NewGuid(); var request=new BusinessRequestInput(id,"Shop","Cebu",AccountFixture.Phone());
        await service.Request(owner.Uid,request); await service.Request(owner.Uid,request);
        await Assert.ThrowsAsync<DomainException>(()=>service.Request(owner.Uid,request with {Id=Guid.NewGuid()}));
        await Assert.ThrowsAsync<DomainException>(()=>service.Decide(owner.Uid,id,new(true,null)));
        await Assert.ThrowsAsync<DomainException>(()=>service.Decide(admin.Uid,id,new(false," ")));
        await service.Decide(admin.Uid,id,new(false,"Clarify location"));
        var next=Guid.NewGuid(); await service.Request(owner.Uid,request with {Id=next,DefaultLocation="Cebu City"});
        await Task.WhenAll(service.Decide(admin.Uid,next,new(true,null)),service.Decide(admin.Uid,next,new(true,null)));
        var overview=await service.Overview(owner.Uid); Assert.Single(overview.Businesses); Assert.Equal(2,overview.Requests.Count);
        Assert.Empty((await new BusinessService(connections,identities).Bootstrap(owner.Uid,next)).Data.Items);
        await service.SavePhone(admin.Uid,new(AccountFixture.Phone()));
        var forbidden=await Assert.ThrowsAsync<DomainException>(()=>new BusinessService(connections,identities).Bootstrap(admin.Uid,next));
        Assert.Equal("forbidden",forbidden.Code);
        await Assert.ThrowsAsync<DomainException>(()=>new ReportQueries(connections).Query(admin.Uid,next,"summary","2026-01-01","2026-12-31"));
        await Assert.ThrowsAsync<DomainException>(()=>service.Decide(admin.Uid,next,new(false,"Late rejection")));
    }
}
