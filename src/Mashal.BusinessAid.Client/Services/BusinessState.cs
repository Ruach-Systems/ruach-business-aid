using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Client.Services;

public sealed class BusinessState(OfflineStorage storage, ApiClient api, SyncService sync, IJSRuntime js, IWebAssemblyHostEnvironment environment, IConfiguration configuration)
{
    public event Action? Changed;
    public OfflineState? State { get; private set; }
    public AppData Data => State?.Data ?? new();
    public ClientUser? User => State?.User;
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
    public IEnumerable<InventoryItem> Inventory => Data.InventoryItems.Where(x => x.DeletedAt is null && x.IsActive);
    public IEnumerable<Product> Products => Data.Products.Where(x => x.DeletedAt is null && x.IsActive);
    public IEnumerable<Sale> Sales => Data.Sales.Where(x => x.DeletedAt is null);
    public IEnumerable<Expense> Expenses => Data.Expenses.Where(x => x.DeletedAt is null);
    private DotNetObjectReference<BusinessState>? reference;
    private void Notify() => Changed?.Invoke();
    private void Update(OfflineState value) { State = value; Notify(); }
    public async Task Initialize()
    {
        Ready = false; StartupError = false; Error = "";
        try
        {
            if (reference is null) { reference = DotNetObjectReference.Create(this); Online = await js.InvokeAsync<bool>("mashalStorage.listen", reference); }
            var account = await storage.Read<ClientUser>("meta", "active-user");
            if (Online) { try { account = await api.Get<ClientUser>("/api/auth/session"); } catch (HttpRequestException) { } catch (TaskCanceledException) { } catch (ApiException) { } }
            if (account is not null && (!account.IsDemo || Development)) await Hydrate(account);
        }
        catch (Exception error) { Error = error.Message; StartupError = Data.Business is null; }
        finally { Ready = true; Notify(); }
    }
    private async Task Hydrate(ClientUser account)
    {
        State = await storage.Read<OfflineState>("accounts", account.Uid) ?? new() { User = account };
        if (!account.IsDemo && Online && State.Data.Business is null)
        {
            var result = await api.Get<BootstrapResult>("/api/bootstrap");
            State = await storage.Mutate(State, latest => { if (result.Cursor >= latest.Cursor) { latest.ServerData = result.Data; latest.Cursor = result.Cursor; latest.Rebuild(); } return Task.CompletedTask; });
        }
        await storage.Write("meta", "active-user", account);
        Notify();
        _ = Synchronize();
    }
    public async Task ContinueLocally()
    {
        if (!Development) throw new InvalidOperationException("Local mode is available only in development.");
        await Hydrate(new("local-owner", "Local business owner", "local@mashal.app", null, true));
        Ready = true; Notify();
    }
    public void SignIn() => api.SignIn();
    public async Task Logout()
    {
        if (Busy) throw new InvalidOperationException("Wait for synchronization to finish.");
        if (User?.IsDemo != true) await api.Post("/api/auth/logout", new { });
        await storage.Write<ClientUser?>("meta", "active-user", null);
        State = null; Error = ""; SyncStatus = "local"; Notify();
    }
    public async Task ResetLocalData()
    {
        if (!CanResetLocalData) throw new InvalidOperationException("Local data can only be reset in development local mode.");
        if (Busy) throw new InvalidOperationException("Wait for synchronization to finish.");
        var account = User!;
        await storage.Delete("accounts", account.Uid);
        State = new OfflineState { User = account };
        await storage.Write("accounts", account.Uid, State);
        await storage.Write("meta", "active-user", account);
        Error = ""; SyncStatus = "local"; Notify();
    }
    [JSInvokable] public async Task ConnectivityChanged(bool online) { Online = online; Notify(); if (online && Ready) await Synchronize(); }
    public async Task Synchronize()
    {
        if (Busy || State is null || User?.IsDemo == true || !Online || Data.Business is null) return;
        Busy = true; SyncStatus = "syncing"; Notify();
        var key = "mashal-sync-" + State.User.Uid;
        try
        {
            await js.InvokeVoidAsync("mashalStorage.acquire", key);
            State = await sync.Synchronize(State, Update);
            Error = ""; SyncStatus = PendingCount > 0 ? "local" : "synced";
        }
        catch (Exception e) { Error = e is ApiException { Status: 401 } ? "Sign in again to synchronize your saved work." : e.Message; SyncStatus = Online ? "error" : "local"; }
        finally { await js.InvokeVoidAsync("mashalStorage.release", key); Busy = false; Notify(); }
    }
    public async Task Execute(string type, Guid id, object payload, string? expectedVersion = null)
    {
        if (State is null) throw new InvalidOperationException("Sign in first.");
        var json = JsonSerializer.SerializeToElement(payload, Wire.Json);
        var operationId = Guid.NewGuid();
        State = await storage.Mutate(State, async latest =>
        {
            var target = type == "adjustStock" ? json.GetProperty("inventoryItemId").GetGuid() : id;
            var op = new PendingOperation { Id = operationId, EntityId = id, Type = type, Payload = json, ExpectedVersion = expectedVersion };
            op.DependsOnPending = latest.Outbox.Any(x => x.EntityId == target || x.Projections.Any(p => p.Entity.GetProperty("id").GetGuid() == target));
            if (type == "createBusiness") latest.Data.Business = Wire.Read<Business>(json);
            else
            {
                var projected = LocalData.Clone(latest.Data);
                var repository = new LocalRepository(projected);
                // The current local projection may include earlier unsynchronized edits.
                var localCommand = op.Command() with { ExpectedVersion = LocalData.Version(projected, target) };
                await new CommandProcessor(repository, checkConcurrency: false).Execute(localCommand);
                op.Projections = repository.Changes;
            }
            latest.Outbox.Add(op);
            latest.Rebuild();
        });
        Notify(); _ = Synchronize();
    }
    public async Task CreateBusiness(string name, string location, bool samples)
    {
        var id = Guid.NewGuid();
        var value = new Business { Id = id, OwnerUid = Guid.TryParse(User!.Uid, out var uid) ? uid : Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = Rules.Name(name), DefaultLocation = Rules.Name(location), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await Execute("createBusiness", id, value);
        if (samples) foreach (var sample in new[] { ("Buko Juice", 3500L, 1630L), ("Empanada", 2500L, 1400L), ("Siomai", 1000L, 550L) })
            await SaveProduct(new() { Id = Guid.NewGuid(), Name = sample.Item1, SellingPriceCentavos = sample.Item2, ManualCostCentavos = sample.Item3, InventoryMode = "untracked", IsActive = true }, null);
    }
    public async Task SaveProduct(Product value, string? version)
    {
        value.Name = Rules.Name(value.Name);
        Rules.Require(!Data.Products.Any(x => x.Id != value.Id && x.DeletedAt is null && Rules.Name(x.Name).Equals(value.Name, StringComparison.OrdinalIgnoreCase)), "A product with this name already exists.");
        if (value.InventoryMode == "prepared" && value.FinishedInventoryItemId is null)
        {
            var item = new InventoryItem { Id = Guid.NewGuid(), Name = value.Name, BaseUnit = "pc", UnitKind = "count", IsActive = true, MinimumQuantity = 5 };
            await Execute("saveInventoryItem", item.Id, item);
            value.FinishedInventoryItemId = item.Id;
        }
        if (value.InventoryMode == "untracked") { value.Recipe = []; value.RecipeBatchYield = 1; }
        await Execute("saveProduct", value.Id, value, version);
    }
    public async Task ReviewConflicts()
    {
        if (State is null || Busy) return;
        if (State.Outbox.FirstOrDefault()?.Type == "createBusiness")
        {
            var result = await api.Get<BootstrapResult>("/api/bootstrap");
            if (result.User.Uid.ToString() != State.User.Uid) throw new InvalidOperationException("Sign in with the account that owns these changes.");
            State = await storage.Mutate(State, latest => { latest.ServerData = result.Data; latest.Cursor = result.Cursor; latest.Rebuild(); return Task.CompletedTask; });
        }
        else State = await sync.Pull(State);
        Notify();
    }
    public async Task ResolveConflict(bool discard)
    {
        await ReviewConflicts();
        State = await storage.Mutate(State!, latest =>
        {
            if (discard) latest.Outbox.Clear();
            else if (latest.Outbox.FirstOrDefault() is { } head)
            {
                Rules.Require(head.Type != "createBusiness", "This account already has a business. Use the server records to continue.");
                head.Id = Guid.NewGuid(); head.Error = null; head.Prepared = false; head.DependsOnPending = true;
            }
            latest.Rebuild(); return Task.CompletedTask;
        });
        Notify(); _ = Synchronize();
    }
}
