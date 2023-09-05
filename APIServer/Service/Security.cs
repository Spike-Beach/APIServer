using System.Security.Cryptography;

namespace APIServer.Service;

public static class Security
{
    static readonly Int16 RepeatCnt = 4357;
    public static (byte[], byte[]) GetSaltAndHashedPassword(String rawPassword)
    {
        byte[] saltBytes = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        // https://learn.microsoft.com/ko-kr/dotnet/api/system.security.cryptography.rfc2898derivebytes?view=net-7.0
        Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(rawPassword, saltBytes, RepeatCnt, HashAlgorithmName.SHA256);
        byte[] hashedPasswordBytes = pbkdf2.GetBytes(31); // 32 -> 31
        return (saltBytes, hashedPasswordBytes);
    }

    public static bool VerifyHashedPassword(String rawPassword, byte[] saltBytes, byte[] hashedPasswordBytes)
    {
        Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(rawPassword, saltBytes, RepeatCnt, HashAlgorithmName.SHA256);
        byte[] newHashed = pbkdf2.GetBytes(31);
        return hashedPasswordBytes.SequenceEqual(newHashed);
    }

    public static String GenerateToken()
    {
        byte[] tokenBytes = new byte[31];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes).Replace("+", "").Replace("/", "");
    }
}