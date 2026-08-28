namespace Worfair.Modules.Identity.Infrastructure.Security;

using Worfair.Modules.Identity.Application.Ports;

/// <summary>BCrypt (docs/security/01 §2: argon2id/bcrypt) — work factor 12.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string hash, string password)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
