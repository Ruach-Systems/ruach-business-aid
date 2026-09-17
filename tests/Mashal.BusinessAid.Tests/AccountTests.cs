using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Client.Services;
using Xunit;
namespace Mashal.BusinessAid.Tests;

public class AccountTests
{
    [Theory]
    [InlineData("09171234567")]
    [InlineData("639171234567")]
    [InlineData("+639171234567")]
    [InlineData(" +63 (917) 123-4567 ")]
    public void MobileFormatsNormalizeToOneIdentity(string value) => Assert.Equal("+639171234567", PhilippinePhone.Normalize(value));
    [Theory]
    [InlineData("")]
    [InlineData("+12025550123")]
    [InlineData("0281234567")]
    [InlineData("0917123456")]
    [InlineData("091712345678")]
    [InlineData("09171234567 ext 1")]
    [InlineData("+639١٧١٢٣٤٥٦٧")]
    [InlineData("++639171234567")]
    [InlineData("09171234567\n123")]
    public void InvalidMobileNumbersAreRejected(string value) => Assert.Throws<DomainException>(()=>PhilippinePhone.Normalize(value));
    [Fact]
    public void WorkspaceKeysSeparateSameOwnerQueues()
    {
        var owner=new ClientUser("owner","Owner","owner@example.invalid",null);
        var a=new OfflineState { User=owner,WorkspaceId=Guid.NewGuid() };
        var b=new OfflineState { User=owner,WorkspaceId=Guid.NewGuid() };
        Assert.NotEqual(a.StorageKey,b.StorageKey);
        Assert.StartsWith("owner:",a.StorageKey);
        Assert.Equal("owner",new OfflineState { User=owner }.StorageKey);
    }
    [Theory]
    [InlineData(true,false,false,"/admin","/businesses")]
    [InlineData(false,true,false,"/admin","/admin")]
    [InlineData(true,false,false,"/sales","/businesses")]
    [InlineData(true,false,false,"/settings","/settings")]
    [InlineData(false,false,true,"/","/onboarding")]
    [InlineData(false,true,true,"/","/admin")]
    public void AccountRoutesKeepRecoveryAndAdminSeparate(bool phoneRequired,bool admin,bool fresh,string path,string expected)
        => Assert.Equal(expected,Calculations.AccountRoute(true,true,true,phoneRequired,admin,fresh,path));
}
