using Microsoft.AspNetCore.Components.Web;
using System.Reflection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Ruach.BusinessAid.Client;
using Ruach.BusinessAid.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
var apiOrigin = typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "ApiOrigin")?.Value
    ?? builder.Configuration["ApiOrigin"] ?? "https://api.businessaid.ruachsystems.dev";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiOrigin), Timeout = TimeSpan.FromSeconds(20) });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<OfflineStorage>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<BusinessState>();
await builder.Build().RunAsync();
