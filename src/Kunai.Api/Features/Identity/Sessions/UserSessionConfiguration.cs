using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kunai.Api.Features.Identity.Sessions;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> session)
    {
        session.ToTable("user_sessions");
        session.Property(s => s.Id).ValueGeneratedNever();
        session.Property(s => s.ClientType).HasConversion<string>().HasMaxLength(16);
        session.Property(s => s.DeviceName).HasMaxLength(UserSession.DeviceNameMaxLength);
        session.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        session.HasIndex(s => s.UserId);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> token)
    {
        token.ToTable("refresh_tokens");
        token.Property(t => t.Id).ValueGeneratedNever();
        token.Property(t => t.TokenHash).HasMaxLength(64);
        token.HasIndex(t => t.TokenHash).IsUnique();
        token.HasOne(t => t.Session).WithMany().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
