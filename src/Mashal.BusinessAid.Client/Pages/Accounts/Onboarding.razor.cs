namespace Mashal.BusinessAid.Client.Pages;

public partial class Onboarding
{
    private string name = "", location = "Main Location", phone = "";
    private readonly Guid requestId = Guid.NewGuid();
    protected override void OnInitialized() { base.OnInitialized(); phone = Store.User?.PhoneNumber ?? ""; }
    private Task Submit() => Store.User?.IsDemo == true
        ? Run(() => Store.CreateLocalBusiness(name, location), "/dashboard")
        : Run(() => Store.RequestBusiness(requestId, name, location, phone), "/businesses");
}
