using System.Text.Json;
using System.Text.Json.Serialization;

using Kunai.Api.Common.Email;
using Kunai.Api.Common.Http;
using Kunai.Api.Common.OpenApi;
using Kunai.Api.Features.Identity;
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

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseKunaiSecurityHeaders();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Kunai API"));

app.MapGet("/", () => "Kunai API").ExcludeFromDescription();

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();

await app.RunAsync();
