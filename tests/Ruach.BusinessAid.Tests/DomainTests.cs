using Ruach.BusinessAid.Shared;
using Xunit;
namespace Ruach.Tests;

public class DomainTests
{
    [Fact] public void CompatibilityCharactersNormalizeLikeTheClient() => Assert.Equal("Milk", Rules.Name("Ｍｉｌｋ"));
    [Fact] public void NamesCollapseWhitespace() => Assert.Equal("Fresh Milk", Rules.Name(" Fresh   Milk "));
    [Fact] public void RoundingIsExplicit() => Assert.Equal(101, Rules.Round(100.5m));
    [Fact] public void InvalidNameIsRejected() => Assert.Throws<DomainException>(() => Rules.Name(" "));
    [Fact]
    public void StaleVersionsAreRejected()
    {
        var item = new Item { Version = [1, 2, 3] };
        Assert.Throws<DomainException>(() => Ruach.BusinessAid.Api.Data.EntityRepository.CheckVersion(item, null));
        Ruach.BusinessAid.Api.Data.EntityRepository.CheckVersion(item, Convert.ToBase64String(item.Version));
    }
}
