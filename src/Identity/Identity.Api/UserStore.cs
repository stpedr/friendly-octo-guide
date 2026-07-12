using System.Collections.Concurrent;
using System.Security.Cryptography;
using Identity.Domain.Totp;

namespace Identity.Api;

public sealed class UserAccount
{
    public required string Username { get; init; }
    public required byte[] PasswordSalt { get; init; }
    public required byte[] PasswordHash { get; init; }
    public required byte[] TotpSeed { get; init; }        // em prod: cifrada, chave no OpenBao
    public required string[] Roles { get; init; }
    public required Dictionary<string, string> Attributes { get; init; }
    public long LastAcceptedTotpStep { get; set; }         // proteção contra replay do código
}

public interface IUserStore
{
    UserAccount? Find(string username);
}

/// <summary>
/// Fase 0: usuários em memória, seed fixa pra dev poder logar com um authenticator real.
/// Fase 1 troca por Postgres — a interface não muda.
/// </summary>
public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<string, UserAccount> _users = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryUserStore()
    {
        Seed("admin", "admin-dev", ["admin"], new() { ["planta"] = "A" });
        Seed("operador", "operador-dev", ["operador"], new() { ["planta"] = "A", ["linha"] = "2", ["turno"] = "dia" });
    }

    public UserAccount? Find(string username) => _users.GetValueOrDefault(username);

    private void Seed(string username, string password, string[] roles, Dictionary<string, string> attributes)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        // Seed TOTP determinística em dev pra facilitar provisionar o authenticator; NUNCA em prod.
        var totpSeed = System.Text.Encoding.ASCII.GetBytes($"dev-seed-{username}-0123");
        _users[username] = new UserAccount
        {
            Username = username,
            PasswordSalt = salt,
            PasswordHash = PasswordHasher.Hash(password, salt),
            TotpSeed = totpSeed,
            Roles = roles,
            Attributes = attributes,
        };
    }
}

public static class PasswordHasher
{
    // PBKDF2-SHA256, 210k iterações (OWASP 2023+). Argon2id entra quando o pacote for aprovado no supply chain.
    public static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);

    public static bool Verify(string password, byte[] salt, byte[] expected) =>
        CryptographicOperations.FixedTimeEquals(Hash(password, salt), expected);
}

/// <summary>Endpoint de provisionamento devolve a URI otpauth:// pro QR do authenticator.</summary>
public static class TotpProvisioning
{
    public static string OtpAuthUri(UserAccount user) =>
        $"otpauth://totp/plataforma-linha:{user.Username}?secret={Base32.Encode(user.TotpSeed)}&issuer=plataforma-linha&digits=6&period=30";
}
