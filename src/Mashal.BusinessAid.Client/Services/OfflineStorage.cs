using System.Text.Json;
using Microsoft.JSInterop;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Client.Services;

// Compare-and-swap keeps the IndexedDB transaction synchronous and retries C# work if another tab saved.
public class OfflineStorage(IJSRuntime js)
{
    public virtual async Task<T?> Read<T>(string store, string key)
    {
        var value = await js.InvokeAsync<string?>("mashalStorage.read", store, key);
        if (value is null) return default;
        // The former development account used a string owner ID; it never synchronizes with SQL.
        value = value.Replace("\"ownerUid\":\"local-owner\"", "\"ownerUid\":\"00000000-0000-0000-0000-000000000001\"");
        return JsonSerializer.Deserialize<T>(value, Wire.Json);
    }
    public virtual ValueTask Write<T>(string store, string key, T value) => js.InvokeVoidAsync("mashalStorage.write", store, key, JsonSerializer.Serialize(value, Wire.Json));
    public virtual ValueTask Delete(string store, string key) => js.InvokeVoidAsync("mashalStorage.delete", store, key);
    public virtual async Task<OfflineState> Mutate(OfflineState fallback, Func<OfflineState, Task> change)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var state = await Read<OfflineState>("accounts", fallback.User.Uid) ?? LocalData.Clone(fallback);
            var revision = state.Revision;
            await change(state);
            state.Revision++;
            if (await js.InvokeAsync<bool>("mashalStorage.compareAndSwap", state.User.Uid, revision, JsonSerializer.Serialize(state, Wire.Json))) return state;
        }
        throw new InvalidOperationException("Another tab is saving. Please try again.");
    }
}
