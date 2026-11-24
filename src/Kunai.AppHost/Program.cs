var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with a non-Turkish locale (C.UTF-8), so case-insensitive
// comparisons never hit the Turkish I/ı rules. Data survives restarts.
var postgres = builder.AddPostgres("postgres")
    .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8 --locale=C.UTF-8")
    .WithDataVolume()
    .WithPgAdmin();

var kunaiDb = postgres.AddDatabase("kunaidb", databaseName: "kunai");

// Catches every email the API sends during development.
var mailpit = builder.AddMailPit("mailpit");

var api = builder.AddDotnetProject("api", "../Kunai.Api/Kunai.Api.csproj")
    .WithReference(kunaiDb)
    .WaitFor(kunaiDb)
    .WithReference(mailpit)
    .WithHttpHealthCheck("/health/ready");

// The Blazor app; it serves the UI and forwards /api to the API, so the browser sees one origin.
var web = builder.AddDotnetProject("web", "../Kunai.Web/Kunai.Web.csproj")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health/ready");

// Links in emails (confirm, reset password) point to the web app.
api.WithEnvironment("App__WebBaseUrl", web.GetEndpoint("https"));

builder.Build().Run();
