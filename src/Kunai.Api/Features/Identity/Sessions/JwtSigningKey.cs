using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

namespace Kunai.Api.Features.Identity.Sessions;

/// <summary>The ES256 key that signs access tokens.</summary>
public sealed class JwtSigningKey : IDisposable
{
    private readonly ECDsa _ecdsa;

    private JwtSigningKey(ECDsa ecdsa)
    {
        _ecdsa = ecdsa;
        Key = new ECDsaSecurityKey(ecdsa) { KeyId = Base64UrlEncoder.Encode(SHA256.HashData(ecdsa.ExportSubjectPublicKeyInfo()))[..16] };
        Credentials = new SigningCredentials(Key, SecurityAlgorithms.EcdsaSha256);
    }

    public ECDsaSecurityKey Key { get; }

    public SigningCredentials Credentials { get; }

    /// <summary>
    /// Loads the configured key. Development and Testing may run without one: an in-memory key is generated,
    /// so mobile tokens stop working after a restart, which is fine locally.
    /// </summary>
    public static JwtSigningKey Create(JwtOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        if (!string.IsNullOrWhiteSpace(options.SigningKeyPem))
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(options.SigningKeyPem);
            return new JwtSigningKey(ecdsa);
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return new JwtSigningKey(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        }

        throw new InvalidOperationException($"{JwtOptions.SectionName}:SigningKeyPem must be configured outside Development.");
    }

    public void Dispose() => _ecdsa.Dispose();
}
