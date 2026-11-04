using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CalendarPlaybook.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for cidaas (spec section 12): a locally generated RSA key, published as a JWKS in a static OpenID
/// configuration, and tokens signed with it. JwtBearer still validates issuer, audience, signature and lifetime.
/// </summary>
public static class TestTokens
{
    public const string BaseUrl = "https://calendar-test.cidaas.test";
    public const string Audience = "https://calendar-api.test";
    public const string Issuer = BaseUrl;

    private static readonly SecurityKey SigningKey = CreateKey("test-signing-key");

    /// <summary>A key the API does not know: tokens signed with it must be rejected.</summary>
    public static readonly SecurityKey ForeignKey = CreateKey("foreign-key");

    /// <summary>What JwtBearer would fetch from <c>{BaseUrl}/.well-known/openid-configuration</c> and its JWKS.</summary>
    public static readonly OpenIdConnectConfiguration OpenIdConfiguration = CreateOpenIdConfiguration();

    public static string Create(
        string? sub,
        string audience = Audience,
        string issuer = Issuer,
        DateTime? expiresUtc = null,
        SecurityKey? key = null)
    {
        var expires = expiresUtc ?? DateTime.UtcNow.AddMinutes(10);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(sub is null ? [] : [new Claim("sub", sub)]),
            IssuedAt = expires.AddMinutes(-20),
            NotBefore = expires.AddMinutes(-20),
            Expires = expires,
            SigningCredentials = new SigningCredentials(key ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }

    private static SecurityKey CreateKey(string keyId)
    {
        return new RsaSecurityKey(RSA.Create(2048)) { KeyId = keyId };
    }

    private static OpenIdConnectConfiguration CreateOpenIdConfiguration()
    {
        // Round-trip the public key through JWKS JSON, as cidaas serves it.
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey((RsaSecurityKey)SigningKey);
        var publicJwk = new JsonWebKey { Kty = jwk.Kty, Kid = jwk.Kid, Use = "sig", Alg = SecurityAlgorithms.RsaSha256, N = jwk.N, E = jwk.E };
        var jwks = new JsonWebKeySet($$"""{"keys":[{{System.Text.Json.JsonSerializer.Serialize(publicJwk)}}]}""");

        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer, JsonWebKeySet = jwks };
        foreach (var signingKey in jwks.GetSigningKeys())
        {
            configuration.SigningKeys.Add(signingKey);
        }

        return configuration;
    }
}
