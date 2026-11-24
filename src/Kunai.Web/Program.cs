using Kunai.Web.Components;

using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// The browser talks to a single origin: this host serves the app and forwards /api/* to the API.
// That keeps the API's httpOnly session cookie first-party, with no CORS. Aspire resolves "api".
var apiAddress = builder.Configuration["Api:BaseUrl"] ?? "https+http://api";
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [new RouteConfig { RouteId = "api", ClusterId = "api", Match = new RouteMatch { Path = "/api/{**rest}" } }],
        [
            new ClusterConfig
            {
                ClusterId = "api",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal) { ["api"] = new() { Address = apiAddress } },
            },
        ])
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapReverseProxy();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Kunai.Web.Client._Imports).Assembly);

await app.RunAsync();
