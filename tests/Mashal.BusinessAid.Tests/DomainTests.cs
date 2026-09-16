using Mashal.BusinessAid.Shared;
using Xunit;
namespace Mashal.Tests;

public class DomainTests
{
    [Fact] public void CompatibilityCharactersNormalizeLikeTheClient() => Assert.Equal("Milk", Rules.Name("Ｍｉｌｋ"));
    [Fact] public void NamesCollapseWhitespace() => Assert.Equal("Fresh Milk", Rules.Name(" Fresh   Milk "));
    [Fact] public void AverageUsesPurchaseValue() => Assert.Equal(150, Rules.Average(10, 100, 10, 2000));
    [Fact] public void RoundingIsExplicit() => Assert.Equal(101, Rules.Round(100.5m));
    [Fact] public void InvalidNameIsRejected() => Assert.Throws<DomainException>(() => Rules.Name(" "));
    [Fact]
    public void StaleVersionsAreRejected()
    {
        var item = new InventoryItem { Version = [1, 2, 3] };
        Assert.Throws<DomainException>(() => Mashal.BusinessAid.Api.Data.EntityRepository.CheckVersion(item, null));
        Mashal.BusinessAid.Api.Data.EntityRepository.CheckVersion(item, Convert.ToBase64String(item.Version));
    }
}
