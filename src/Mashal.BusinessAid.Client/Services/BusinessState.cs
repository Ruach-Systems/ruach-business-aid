using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Services;
// The partial files share one state instance and gate: account changes, edits and sync stay serialized.
public sealed partial class BusinessState(OfflineStorage storage, ApiClient api, SyncService sync, IJSRuntime js, IWebAssemblyHostEnvironment environment, IConfiguration configuration)
{
    public event Action? Changed;
    public OfflineState? State { get; private set; }
    public AppData Data => PhoneRequired ? new() : State?.Data ?? new();
    public ClientUser? User { get; private set; }
    public AccountOverview? Account { get; private set; }
    public bool PhoneRequired => User is { IsDemo: false, PhoneNumber: null };
    public bool Ready { get; private set; }
    public bool StartupError { get; private set; }
    public bool Online { get; private set; } = true;
    public bool Busy { get; private set; }
    public string Error { get; private set; } = "";
    public string SyncStatus { get; private set; } = "local";
    public bool Development => environment.IsDevelopment();
    public bool ShowReset => Development && configuration.GetValue<bool>("EnableSignInReset");
    public bool CanResetLocalData => Development && User?.IsDemo == true;
    public int PendingCount => State?.Outbox.Count ?? 0;
    public IEnumerable<Item> AllItems => Data.Items.Where(x => x.DeletedAt is null);
    public IEnumerable<Item> Items => AllItems.Where(x => x.IsActive);
    public IEnumerable<Sale> Sales => Data.Sales.Where(x => x.DeletedAt is null);
    public IEnumerable<Expense> Expenses => Data.Expenses.Where(x => x.DeletedAt is null);

    private readonly SemaphoreSlim gate = new(1, 1);
    private DotNetObjectReference<BusinessState>? reference;
    private void Notify() => Changed?.Invoke();
    private void Update(OfflineState value)
    {
        State = value;
        Notify();
    }

    private static ClientUser Client(AppUser u) => new(u.Uid.ToString(), u.DisplayName, u.Email, u.PhotoURL)
    {
        PhoneNumber = u.PhoneNumber,
        IsAdmin = u.IsAdmin
    };
}
