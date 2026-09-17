using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace APIGateWay.BusinessLayer.Helpers
{
    /// <summary>
    /// Encrypts/decrypts the escrowed chat recovery code with a server-held admin key
    /// (config "ChatRecoveryEscrow:key" — separate from EncryptionKey so it can be rotated
    /// and restricted independently). Whoever holds this key can decrypt any user's chat
    /// recovery code, and therefore their chat private key — treat it like a master secret.
    /// Blob layout: [12-byte IV | 16-byte AES-GCM tag | ciphertext], base64-encoded.
    /// </summary>
    public interface IChatRecoveryEscrowCipher
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    public class ChatRecoveryEscrowCipher : IChatRecoveryEscrowCipher
    {
        private const int IvLength = 12;
        private const int TagLength = 16;

        private readonly byte[] _key;

        public ChatRecoveryEscrowCipher(IConfiguration configuration)
        {
            var configuredKey = configuration["ChatRecoveryEscrow:key"];
            if (string.IsNullOrEmpty(configuredKey))
                throw new InvalidOperationException("Configuration \"ChatRecoveryEscrow:key\" is required.");

            // Hashed to a fixed 32 bytes so the configured value doesn't need to be an exact AES-256 key length.
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        }

        public string Encrypt(string plainText)
        {
            var iv = RandomNumberGenerator.GetBytes(IvLength);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagLength];

            using var gcm = new AesGcm(_key, TagLength);
            gcm.Encrypt(iv, plainBytes, cipherBytes, tag);

            var result = new byte[IvLength + TagLength + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, IvLength);
            Buffer.BlockCopy(tag, 0, result, IvLength, TagLength);
            Buffer.BlockCopy(cipherBytes, 0, result, IvLength + TagLength, cipherBytes.Length);
            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            var data = Convert.FromBase64String(cipherText);
            if (data.Length < IvLength + TagLength)
                throw new CryptographicException("Escrowed recovery code blob is too short.");

            var iv = data[..IvLength];
            var tag = data[IvLength..(IvLength + TagLength)];
            var cipherBytes = data[(IvLength + TagLength)..];
            var plainBytes = new byte[cipherBytes.Length];

            using var gcm = new AesGcm(_key, TagLength);
            gcm.Decrypt(iv, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
