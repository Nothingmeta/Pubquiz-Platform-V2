using System.Security.Cryptography;
using System.Text;

namespace Pubquiz_Platform_V2.Services
{
    public interface ISecretCryptoService
    {
        string Encrypt(string plaintext);
        string Decrypt(string payload);
    }

    public sealed class SecureStringCryptoService : ISecretCryptoService
    {
        private readonly byte[] _key;

        public SecureStringCryptoService(IConfiguration configuration)
        {
            var base64Key = configuration["Crypto:MasterKey"];

            if (string.IsNullOrWhiteSpace(base64Key))
            {
                throw new InvalidOperationException("Crypto:MasterKey is not configured.");
            }

            _key = Convert.FromBase64String(base64Key);

            if (_key.Length != 32)
            {
                throw new InvalidOperationException("Crypto:MasterKey must be a 32-byte key encoded as Base64.");
            }
        }

        public string Encrypt(string plaintext)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[16];

            using var aes = new AesGcm(_key);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

            return Convert.ToBase64String(payload);
        }

        public string Decrypt(string payload)
        {
            var buffer = Convert.FromBase64String(payload);

            if (buffer.Length < 12 + 16)
            {
                throw new CryptographicException("Invalid encrypted payload.");
            }

            var nonce = buffer[..12];
            var tag = buffer[12..28];
            var ciphertext = buffer[28..];
            var plaintextBytes = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key);
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }
}