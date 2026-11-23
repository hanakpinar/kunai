using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kunai.Api.Features.Identity;

/// <summary>The admin and moderator roles exist in every database.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> role) =>
        role.HasData(
            new IdentityRole<Guid>
            {
                Id = new Guid("0193e2a0-0000-7000-8000-000000000001"),
                Name = Roles.Admin,
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "8b0e7f4e-admin",
            },
            new IdentityRole<Guid>
            {
                Id = new Guid("0193e2a0-0000-7000-8000-000000000002"),
                Name = Roles.Moderator,
                NormalizedName = "MODERATOR",
                ConcurrencyStamp = "8b0e7f4e-moderator",
            });
}
