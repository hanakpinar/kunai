using Kunai.Api.Features.Identity;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kunai.Api.Persistence;

/// <summary>
/// The single database context. Features add their own entity configurations
/// (IEntityTypeConfiguration classes) and they are picked up automatically.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("pg_trgm");

        builder.Entity<AppUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.Entity<AppUser>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(AppUser.DisplayNameMaxLength);
            user.Property(u => u.Bio).HasMaxLength(AppUser.BioMaxLength);
            user.Property(u => u.AvatarId).HasMaxLength(32);
            user.Property(u => u.Locale).HasMaxLength(8);
            user.Property(u => u.TimeZone).HasMaxLength(64);
        });

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
