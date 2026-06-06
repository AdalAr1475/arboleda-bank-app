using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Frontend;
using Frontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// URL del backend, cableada en wwwroot/appsettings.json (puerto fijo 5080).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<RecargaApiClient>();

await builder.Build().RunAsync();
