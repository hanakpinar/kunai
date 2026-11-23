using System.Text.Json;
using System.Text.Json.Serialization;

using Kunai.Api.Common.Http;
using Kunai.Api.Common.OpenApi;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddKunaiProblemDetails();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddKunaiOpenApi();
builder.Services.AddKunaiRateLimiting(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseKunaiSecurityHeaders();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Kunai API"));

app.MapGet("/", () => "Kunai API").ExcludeFromDescription();

app.Run();
