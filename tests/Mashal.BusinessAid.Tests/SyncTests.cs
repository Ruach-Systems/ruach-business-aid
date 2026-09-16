using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Client.Services;
using Xunit;
namespace Mashal.BusinessAid.Tests;

public class SyncTests
{
    private static OfflineState Initial()
    {
        var data = new AppData { Business = new() { Id = Guid.NewGuid() } };
        return new() { User = new(Guid.NewGuid().ToString(), "Owner", "test@example.invalid", null), Data = data, ServerData = LocalData.Clone(data), Outbox = [new() { Id = Guid.NewGuid(), EntityId = Guid.NewGuid(), Type = "saveExpense", Payload = JsonSerializer.SerializeToElement(new { description = "Fare", category = "Transportation", amountCentavos = 2000, expenseDate = "2026-09-16" }), Projections = [] }] };
    }
    [Theory]
    [InlineData(401, "unauthorized")]
    [InlineData(503, "retry")]
    public async Task TemporaryFailurePreservesFrozenRequest(int status, string code)
    {
        var state = Initial(); var storage = new MemoryStorage(state); var api = new FakeApi { Failure = new ApiException(status, code, "Try again") };
        var sync = new SyncService(api, storage);
        await Assert.ThrowsAsync<ApiException>(() => sync.Synchronize(state, _ => { }));
        var first = JsonSerializer.Serialize(storage.Current.Outbox.Single().Command(), Wire.Json);
        Assert.True(storage.Current.Outbox.Single().Prepared); Assert.Null(storage.Current.Outbox.Single().Error);
        api.Failure = null; await sync.Synchronize(storage.Current, _ => { });
        Assert.Equal(first, api.Requests.Last()); Assert.Empty(storage.Current.Outbox);
    }
    [Fact]
    public async Task LostResponseRetriesIdenticalOperationAndPullMustFinishBeforeAcknowledgement()
    {
        var state = Initial(); var storage = new MemoryStorage(state); var api = new FakeApi { PullFailure = true }; var sync = new SyncService(api, storage);
        await Assert.ThrowsAsync<HttpRequestException>(() => sync.Synchronize(state, _ => { }));
        Assert.Single(storage.Current.Outbox);
        var sent = api.Requests.Single(); api.PullFailure = false;
        await sync.Synchronize(storage.Current, _ => { });
        Assert.Equal(sent, api.Requests.Last()); Assert.Empty(storage.Current.Outbox);
    }
    [Fact]
    public async Task ConflictStopsQueueAndRetainsAllWork()
    {
        var state = Initial(); state.Outbox.Add(LocalData.Clone(state.Outbox[0])); state.Outbox[1].Id = Guid.NewGuid();
        var storage = new MemoryStorage(state); var api = new FakeApi { Failure = new ApiException(409, "conflict", "Changed elsewhere") }; var sync = new SyncService(api, storage);
        await Assert.ThrowsAsync<ApiException>(() => sync.Synchronize(state, _ => { }));
        Assert.Equal(2, storage.Current.Outbox.Count); Assert.Equal("Changed elsewhere", storage.Current.Outbox[0].Error);
        Assert.Single(api.Requests);
    }
    [Fact]
    public async Task PullPagesAndConcurrentTabEnqueueSurvive()
    {
        var state = Initial(); var storage = new MemoryStorage(state); var api = new FakeApi();
        api.OnPush = () => { if (api.Requests.Count == 1) { var extra = LocalData.Clone(storage.Current.Outbox[0]); extra.Id = Guid.NewGuid(); extra.EntityId = Guid.NewGuid(); extra.Prepared = false; storage.Current.Outbox.Add(extra); } };
        api.Pages.Enqueue(new(1, [], true)); api.Pages.Enqueue(new(2, [], false));
        await new SyncService(api, storage).Synchronize(state, _ => { });
        Assert.Equal(2, api.Requests.Count); Assert.Empty(storage.Current.Outbox); Assert.Equal(2, storage.Current.Cursor);
    }
    private sealed class MemoryStorage(OfflineState initial) : OfflineStorage(null!)
    {
        public OfflineState Current = LocalData.Clone(initial);
        public override async Task<OfflineState> Mutate(OfflineState fallback, Func<OfflineState, Task> change) { var next = LocalData.Clone(Current); await change(next); next.Revision++; Current = next; return LocalData.Clone(Current); }
    }
    private sealed class Navigation : NavigationManager { public Navigation() => Initialize("http://localhost/", "http://localhost/"); protected override void NavigateToCore(string uri, bool forceLoad) { } }
    private sealed class FakeApi() : ApiClient(new HttpClient(), new Navigation())
    {
        public Exception? Failure; public bool PullFailure; public Action? OnPush; public List<string> Requests = []; public Queue<PullResult> Pages = [];
        public override Task Post(string path, object body) { var push = (PushRequest)body; Requests.Add(JsonSerializer.Serialize(push.Operations.Single(), Wire.Json)); OnPush?.Invoke(); if (Failure is not null) throw Failure; return Task.CompletedTask; }
        public override Task<T> Get<T>(string path) { if (PullFailure) throw new HttpRequestException("Connection interrupted"); var cursor = long.Parse(path.Split("cursor=")[1]); return Task.FromResult((T)(object)(Pages.Count > 0 ? Pages.Dequeue() : new PullResult(cursor, [], false))); }
    }
}
