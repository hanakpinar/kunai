using Kunai.Web.Client.Api;
using Kunai.Web.Client.Infrastructure;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Bundle;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var origin = new Uri(builder.HostEnvironment.BaseAddress);

// Same origin as the page: the browser sends the API's httpOnly cookie automatically; writes add X-CSRF.
builder.Services.AddScoped(_ => new HttpClient(new CsrfHandler { InnerHandler = new HttpClientHandler() }) { BaseAddress = origin });
builder.Services.AddScoped(services => new KunaiApiClient(
    new DefaultRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: services.GetRequiredService<HttpClient>())
    {
        BaseUrl = origin.GetLeftPart(UriPartial.Authority),
    }));

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthSession>());
builder.Services.AddScoped<CatalogMetadata>();

await builder.Build().RunAsync();
