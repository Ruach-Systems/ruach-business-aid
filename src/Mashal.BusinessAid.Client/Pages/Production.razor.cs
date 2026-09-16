using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Production
{
    private IEnumerable<Product> Prepared => Store.Products.Where(x => x.InventoryMode == "prepared"); private IEnumerable<ProductionBatch> Drafts => Store.Data.Batches.Where(x => x.DeletedAt is null && x.Status == "draft"); private IEnumerable<ProductionBatch> Completed => Store.Data.Batches.Where(x => x.DeletedAt is null && x.Status == "completed").OrderByDescending(x => x.CompletedAt);
}
