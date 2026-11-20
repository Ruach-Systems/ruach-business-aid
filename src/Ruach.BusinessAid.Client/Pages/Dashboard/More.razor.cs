namespace Ruach.BusinessAid.Client.Pages;

public partial class More
{
    private readonly (string Path, string Label, string Icon)[] links = [("/items", "Items", "cube"), ("/expenses", "Expenses", "receipt"), ("/reports", "Reports", "report"), ("/settings", "Settings", "settings")];
}
