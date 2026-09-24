namespace Mashal.BusinessAid.Client.Pages;

public partial class More
{
    private readonly (string Path, string Label, string Icon)[] links = [("/items", "Items", "tag"), ("/expenses", "Expenses", "receipt"), ("/reports", "Reports", "chart"), ("/settings", "Settings", "settings")];
}
