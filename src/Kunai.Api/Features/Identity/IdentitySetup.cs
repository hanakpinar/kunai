using Kunai.Api.Common;
using Kunai.Api.Persistence;

using Microsoft.AspNetCore.Identity;

namespace Kunai.Api.Features.Identity;

public static class IdentitySetup
{
    public static IHostApplicationBuilder AddKunaiIdentity(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AppOptions>().Bind(builder.Configuration.GetSection(AppOptions.SectionName));

        builder.Services
            .AddIdentityCore<AppUser>(options =>
            {
                // NIST 800-63B: length over composition rules; breached passwords are rejected separately.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789_";

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var pwned = builder.Configuration.GetSection(PwnedPasswordsOptions.SectionName).Get<PwnedPasswordsOptions>()
            ?? new PwnedPasswordsOptions();
        if (pwned.Enabled)
        {
            builder.Services.AddHttpClient<PwnedPasswordValidator>(client =>
            {
                client.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
                client.DefaultRequestHeaders.Add("Add-Padding", "true");
            });
            builder.Services.AddScoped<IPasswordValidator<AppUser>>(services => services.GetRequiredService<PwnedPasswordValidator>());
        }

        builder.Services.AddScoped<IdentityEmails>();
        return builder;
    }
}
