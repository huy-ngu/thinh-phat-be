namespace ThinhPhat.Api.Shared.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // Work factor 11 gives strong cryptographic protection while keeping CPU time ~100-150ms on modest cloud vCPUs
    private const int WorkFactor = 11;

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
