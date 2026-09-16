namespace Mashal.BusinessAid.Client.Pages;

public partial class Onboarding
{
    private string name = "", location = "Main Location"; private bool samples = true; private Task Submit() => Run(() => Store.CreateBusiness(name, location, samples), "/dashboard");
}
