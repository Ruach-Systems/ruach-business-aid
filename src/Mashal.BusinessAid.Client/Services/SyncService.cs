using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Client.Services;

public sealed class SyncService(ApiClient api, OfflineStorage storage)
{
    public async Task<OfflineState> Pull(OfflineState state)
    {
        if (state.Data.Business is null) return state;
        bool more;
        do
        {
            var result = await api.Get<PullResult>($"/api/sync/pull?businessId={state.Data.Business!.Id}&cursor={state.Cursor}&modelVersion={DataModel.CurrentVersion}");
            if (result.ModelVersion != DataModel.CurrentVersion)
                throw new ApiException(426, "client_upgrade_required", "Refresh Business Aid to finish the Items update.");
            state = await storage.Mutate(state, latest =>
            {
                if (result.Cursor >= latest.Cursor)
                {
                    foreach (var change in result.Changes) LocalData.Apply(latest.ServerData, change.CollectionName, change.Payload);
                    latest.Cursor = result.Cursor;
                    latest.Rebuild();
                }
                return Task.CompletedTask;
            });
            more = result.HasMore;
        } while (more);
        return state;
    }
    public async Task<OfflineState> Synchronize(OfflineState state, Action<OfflineState> changed)
    {
        while (true)
        {
            state = await storage.Mutate(state, latest =>
            {
                var first = latest.Outbox.FirstOrDefault();
                if (first is not null && !first.Prepared && first.Error is null)
                {
                    if (first.DependsOnPending) first.ExpectedVersion = LocalData.Version(latest.ServerData, first.VersionTarget);
                    first.Prepared = true;
                }
                return Task.CompletedTask;
            });
            changed(state);
            var op = state.Outbox.FirstOrDefault();
            if (op is null) break;
            if (op.Error is not null) throw new ApiException(409, "conflict", op.Error);
            try { await api.Post("/api/sync/push", new PushRequest(DataModel.CurrentVersion, state.Data.Business!.Id, [op.Command()])); }
            catch (ApiException error) when (error.Status is >= 400 and < 500 && error.Status is not (401 or 403) && error.Code != "csrf")
            {
                state = await storage.Mutate(state, latest => { var failed = latest.Outbox.Find(x => x.Id == op.Id); if (failed is not null) failed.Error = error.Message; return Task.CompletedTask; });
                changed(state);
                throw;
            }
            state = await Pull(state);
            state = await storage.Mutate(state, latest => { latest.Outbox.RemoveAll(x => x.Id == op.Id); latest.Rebuild(); return Task.CompletedTask; });
            changed(state);
        }
        state = await Pull(state); changed(state);
        return state;
    }
}
