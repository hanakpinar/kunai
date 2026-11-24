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
// Pages render only in the browser, which decides who may see them ([Authorize] -> /login) and the API enforces
// access to the data. This host has no authentication, so it must serve the shell for every route; otherwise a
// page's [Authorize] makes the server try to challenge and answer 500 on a reload or deep link.
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Kunai.Web.Client._Imports).Assembly)
    .AllowAnonymous();

await app.RunAsync();
