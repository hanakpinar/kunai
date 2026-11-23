using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kunai.Api.Persistence;

/// <summary>Lets "dotnet ef migrations add" build the model without starting the app or a database.</summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceSetup.Configure(options, "Host=localhost;Database=kunai_design_time");
        return new AppDbContext(options.Options);
    }
}
