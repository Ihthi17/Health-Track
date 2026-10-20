using System.Security.Cryptography;
using System.Text;

namespace HealthTrack.Services
{
    public interface IPasswordResetTokenService
    {
        /// <summary>
        /// Generates a cryptographically secure random token and returns it plaintext 
        /// (for sending to user) and hashed as Base64 (for storing in database)
        /// </summary>
        void GenerateToken(out string plainToken, out string tokenHashBase64);

        /// <summary>
        /// Verifies that a plaintext token matches the stored hash using constant-time comparison
        /// </summary>
        bool VerifyToken(string plainToken, string storedHashBase64);
    }

    public class PasswordResetTokenService : IPasswordResetTokenService
    {
        private const int TokenLengthBytes = 32;

        public void GenerateToken(out string plainToken, out string tokenHashBase64)
        {
            // Generate 32 random bytes
            var randomBytes = new byte[TokenLengthBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            // Convert to URL-safe base64 string
            plainToken = Convert.ToBase64String(randomBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            // Hash the token for storage
            using (var sha256 = SHA256.Create())
            {
                var tokenHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
                tokenHashBase64 = Convert.ToBase64String(tokenHash);
            }
        }

        public bool VerifyToken(string plainToken, string storedHashBase64)
        {
            if (string.IsNullOrEmpty(plainToken) || string.IsNullOrEmpty(storedHashBase64))
                return false;

            try
            {
                // Decode the stored hash from Base64
                var storedHash = Convert.FromBase64String(storedHashBase64);

                // Hash the provided token
                byte[] computedHash;
                using (var sha256 = SHA256.Create())
                {
                    computedHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
                }

                // Use constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
