namespace Mashal.BusinessAid.Client.Pages;

public partial class More
{
    private readonly (string Path, string Label, string Icon)[] links = [("/products", "Products & costing", "tag"), ("/production", "Production", "batch"), ("/expenses", "Expenses", "receipt"), ("/reports", "Reports", "chart"), ("/settings", "Settings", "settings")];
}
