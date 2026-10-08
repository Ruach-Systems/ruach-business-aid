using Microsoft.JSInterop;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Client.Services;
public sealed partial class BusinessState
{
    [JSInvokable]
    public async Task ConnectivityChanged(bool online)
    {
        Online = online;
        Notify();
        if (online && Ready)
            await Synchronize();
    }

    public async Task Synchronize()
    {
        if (User is null || User.IsDemo || !Online || !await gate.WaitAsync(0))
            return;
        Busy = true;
        SyncStatus = "syncing";
        Notify();
        string? key = null;
        try
        {
            await RefreshAccountCore();
            if (State?.Data.Business is null || PhoneRequired)
            {
                SyncStatus = "local";
                return;
            }

            key = "mashal-sync-" + State.StorageKey;
            await js.InvokeVoidAsync("mashalStorage.acquire", key);
            State = await sync.Synchronize(State, Update);
            Error = "";
            SyncStatus = PendingCount > 0 ? "local" : "synced";
        }
        catch (Exception e)
        {
            if (e is ApiException { Code: "phone_required" })
            {
                State = null;
                if (User is not null)
                    User = User with
                    {
                        PhoneNumber = null
                    };
            }

            Error = e is ApiException { Status: 401 } ? "Sign in again to synchronize your saved work." : e.Message;
            SyncStatus = Online ? "error" : "local";
        }
        finally
        {
            try
            {
                if (key is not null)
                    await js.InvokeVoidAsync("mashalStorage.release", key);
            }
            finally
            {
                Busy = false;
                gate.Release();
                Notify();
            }
        }
    }

    public async Task ReviewConflicts()
    {
        await gate.WaitAsync();
        try
        {
            if (State is not null && !PhoneRequired)
                State = await sync.Pull(State);
        }
        finally
        {
            gate.Release();
            Notify();
        }
    }

    public async Task ResolveConflict(bool discard)
    {
        await gate.WaitAsync();
        try
        {
            if (State is null || PhoneRequired)
                return;
            State = await sync.Pull(State);
            State = await storage.Mutate(State, latest =>
            {
                if (discard)
                    latest.Outbox.Clear();
                else if (latest.Outbox.FirstOrDefault()is { } head)
                {
                    head.Id = Guid.NewGuid();
                    head.Error = null;
                    head.Prepared = false;
                    head.DependsOnPending = true;
                }

                latest.Rebuild();
                return Task.CompletedTask;
            });
        }
        finally
        {
            gate.Release();
            Notify();
        }

        _ = Synchronize();
    }
}
