using System.Text.Json;
using System.Text.Json.Serialization;

using Kunai.Api.Common.Email;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.OpenApi;
using Kunai.Api.Features.Catalog;
using Kunai.Api.Features.Identity;
using Kunai.Api.Features.Profiles;
using Kunai.Api.Persistence;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Platform
builder.Services.AddKunaiProblemDetails();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddKunaiOpenApi();
builder.Services.AddKunaiRateLimiting(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// Infrastructure
builder.AddKunaiPersistence();
builder.AddKunaiEmail();

// Features
builder.AddKunaiIdentity();
builder.AddKunaiAuthentication();
builder.AddKunaiCatalog();

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseKunaiSecurityHeaders();
app.UseAuthentication();
app.UseKunaiCsrfProtection();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference("/docs", options => options.WithTitle("Kunai API")).AllowAnonymous();

app.MapGet("/", () => "Kunai API").ExcludeFromDescription().AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapMeEndpoints();
api.MapCatalogEndpoints();

await app.RunAsync();
