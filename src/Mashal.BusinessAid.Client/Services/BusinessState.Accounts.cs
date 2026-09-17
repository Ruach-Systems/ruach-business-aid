using Mashal.BusinessAid.Shared;
using Microsoft.JSInterop;

namespace Mashal.BusinessAid.Client.Services;
public sealed partial class BusinessState
{
    public async Task Initialize()
    {
        Ready = false;
        StartupError = false;
        Error = "";
        try
        {
            if (reference is null)
            {
                reference = DotNetObjectReference.Create(this);
                Online = await js.InvokeAsync<bool>("mashalStorage.listen", reference);
            }

            var account = await storage.Read<ClientUser>("meta", "active-user");
            if (Online && account?.IsDemo != true)
            {
                try
                {
                    account = await api.Get<ClientUser>("/api/auth/session");
                }
                catch (ApiException e)when (e.Status == 401)
                {
                    account = null;
                    await storage.Write<ClientUser?>("meta", "active-user", null);
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException)
                {
                }
            }

            if (account is not null && (!account.IsDemo || Development))
                await Hydrate(account);
            else
            {
                User = null;
                State = null;
                Account = null;
            }
        }
        catch (Exception error)
        {
            Error = error.Message;
            StartupError = true;
        }
        finally
        {
            Ready = true;
            Notify();
        }

        _ = Synchronize();
    }

    private async Task Hydrate(ClientUser account)
    {
        User = account;
        State = null;
        Account = await storage.Read<AccountOverview>("meta", "account:" + account.Uid);
        if (!account.IsDemo && Online)
        {
            try
            {
                await RefreshAccountCore();
            }
            catch (HttpRequestException)when (Account is not null)
            {
            }
            catch (TaskCanceledException)when (Account is not null)
            {
            }
        }

        if (account.IsDemo)
            State = await storage.Read<OfflineState>("accounts", account.Uid) ?? new()
            {
                User = account
            };
        else if (!PhoneRequired)
        {
            var selected = await storage.Read<Guid?>("meta", "selected:" + account.Uid);
            if (selected is { } id && Account?.Businesses.Any(x => x.Id == id) == true)
                await SelectCore(id);
        }

        await storage.Write("meta", "active-user", User);
        Notify();
    }

    private async Task RefreshAccountCore()
    {
        if (User is null || User.IsDemo || !Online)
            return;
        var result = await api.Get<AccountOverview>("/api/account");
        if (result.User.Uid.ToString() != User.Uid)
            throw new InvalidOperationException("Your signed-in account changed. Sign out and sign in again.");
        Account = result;
        User = Client(result.User);
        if (PhoneRequired || State?.Data.Business is { } b && !result.Businesses.Any(x => x.Id == b.Id))
            State = null;
        await storage.Write("meta", "account:" + User.Uid, result);
        await storage.Write("meta", "active-user", User);
    }

    public async Task RefreshAccount()
    {
        await gate.WaitAsync();
        Busy = true;
        Notify();
        try
        {
            await RefreshAccountCore();
        }
        finally
        {
            Busy = false;
            gate.Release();
            Notify();
        }
    }

    private async Task SelectCore(Guid business)
    {
        if (PhoneRequired)
            throw new InvalidOperationException("Add a unique mobile number before opening a business.");
        if (Account?.Businesses.Any(x => x.Id == business) != true)
            throw new InvalidOperationException("This business is not available to your account.");
        var candidate = await storage.Read<OfflineState>("accounts", $"{User!.Uid}:{business}");
        if (candidate is null)
        {
            if (!Online)
                throw new InvalidOperationException("Open this business once while online to make its records available on this device.");
            candidate = new()
            {
                User = User,
                WorkspaceId = business
            };
            var result = await api.Get<BootstrapResult>($"/api/bootstrap?businessId={business}");
            if (result.User.Uid.ToString() != User.Uid || result.Data.Business?.Id != business)
                throw new InvalidOperationException("The server returned a different workspace.");
            candidate = await storage.Mutate(candidate, latest =>
            {
                latest.ServerData = result.Data;
                latest.Cursor = result.Cursor;
                latest.Rebuild();
                return Task.CompletedTask;
            });
        }

        if (candidate.User.Uid != User.Uid || candidate.WorkspaceId != business || candidate.Data.Business?.Id != business)
            throw new InvalidOperationException("The saved workspace does not match this business.");
        State = candidate;
        Error = "";
        SyncStatus = "local";
        await storage.Write("meta", "selected:" + User.Uid, business);
    }

    public async Task SelectBusiness(Guid business)
    {
        await gate.WaitAsync();
        Busy = true;
        Notify();
        try
        {
            await RefreshAccountCore();
            await SelectCore(business);
        }
        finally
        {
            Busy = false;
            gate.Release();
            Notify();
        }

        _ = Synchronize();
    }

    private void RequireOnlineAccount()
    {
        if (!Online || User is null || User.IsDemo)
            throw new InvalidOperationException("Connect and sign in with Google to manage your account.");
    }

    public async Task SavePhone(string phone)
    {
        RequireOnlineAccount();
        var normalized = PhilippinePhone.Normalize(phone);
        await gate.WaitAsync();
        Busy = true;
        Notify();
        try
        {
            await api.Post("/api/account/phone", new PhoneInput(normalized));
            await RefreshAccountCore();
        }
        finally
        {
            Busy = false;
            gate.Release();
            Notify();
        }
    }

    public async Task RequestBusiness(Guid id, string name, string location, string? phone = null)
    {
        RequireOnlineAccount();
        var input = new BusinessRequestInput(id, Rules.Name(name), Rules.Name(location), phone is null ? null : PhilippinePhone.Normalize(phone));
        await gate.WaitAsync();
        Busy = true;
        Notify();
        try
        {
            await api.Post("/api/account/requests", input);
            await RefreshAccountCore();
        }
        finally
        {
            Busy = false;
            gate.Release();
            Notify();
        }
    }

    public async Task ContinueLocally()
    {
        if (!Development)
            throw new InvalidOperationException("Local mode is available only in development.");
        await Hydrate(new("local-owner", "Local business owner", "local@mashal.app", null, true));
        Ready = true;
        Notify();
    }

    public void SignIn() => api.SignIn();
    public async Task Logout()
    {
        await gate.WaitAsync();
        try
        {
            if (User?.IsDemo != true)
                await api.Post("/api/auth/logout", new { });
            await storage.Write<ClientUser?>("meta", "active-user", null);
            State = null;
            User = null;
            Account = null;
            Error = "";
            SyncStatus = "local";
        }
        finally
        {
            gate.Release();
            Notify();
        }
    }

    public async Task ResetLocalData()
    {
        if (!CanResetLocalData)
            throw new InvalidOperationException("Local data can only be reset in development local mode.");
        await gate.WaitAsync();
        try
        {
            await storage.Delete("accounts", User!.Uid);
            State = new OfflineState
            {
                User = User
            };
            await storage.Write("accounts", User.Uid, State);
            Error = "";
            SyncStatus = "local";
        }
        finally
        {
            gate.Release();
            Notify();
        }
    }
}
