using System.Security.Cryptography;

namespace HealthTrack.Services
{
    public static class PasswordHasher
    {
        // PBKDF2 with HMACSHA256
        public static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            
            // Convert to base64 strings
            passwordSalt = Convert.ToBase64String(salt);
            passwordHash = Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string storedHashBase64, string storedSaltBase64)
        {
            try
            {
                // Convert from base64 strings back to bytes
                var storedHash = Convert.FromBase64String(storedHashBase64);
                var storedSalt = Convert.FromBase64String(storedSaltBase64);
                
                var computed = Rfc2898DeriveBytes.Pbkdf2(password, storedSalt, 100_000, HashAlgorithmName.SHA256, 32);
                return CryptographicOperations.FixedTimeEquals(computed, storedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
