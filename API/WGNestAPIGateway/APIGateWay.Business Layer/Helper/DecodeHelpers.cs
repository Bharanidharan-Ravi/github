using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using APIGateWay.ModalLayer;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace APIGateWay.BusinessLayer.Helpers
{
    public class DecodeHelpers
    {
        private readonly IConfiguration _configuration;

        public DecodeHelpers(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public UserInfo DecodeJwtToken(string encryptedToken)
        {
            string decryptedToken = DecryptUserInfo(encryptedToken);
            var userLoginInfo = JsonConvert.DeserializeObject<List<UserInfo>>(decryptedToken);

            // Return the deserialized object
            return userLoginInfo[0];
        }
        private string SanitizeBase64String(string base64String)
        {
            // Remove any non-base64 characters (like spaces or invalid symbols)
            return Regex.Replace(base64String, "[^A-Za-z0-9+/=]", "");
        }
        private bool IsBase64String(string base64String)
        {
            return Regex.IsMatch(base64String, @"^[a-zA-Z0-9\+/]*={0,2}$");
        }
        private string AddBase64Padding(string base64String)
        {
            int padding = base64String.Length % 4;
            if (padding > 0)
            {
                base64String = base64String.PadRight(base64String.Length + (4 - padding), '=');
            }
            return base64String;
        }

        private string DecryptUserInfo(string encryptedText)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_configuration["EncryptionKey:key"]);

            if (!IsBase64String(encryptedText))
            {
                throw new ArgumentException("Invalid Base64 string");
            }

            // Optionally, sanitize and add padding to the Base64 string
            encryptedText = SanitizeBase64String(encryptedText);
            encryptedText = AddBase64Padding(encryptedText);

            // Now it should be a valid Base64 string
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            //byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

            // Extract IV from the encrypted bytes
            byte[] iv = new byte[16];
            Buffer.BlockCopy(encryptedBytes, 0, iv, 0, iv.Length);

            // Extract the actual encrypted content
            byte[] cipherText = new byte[encryptedBytes.Length - iv.Length];
            Buffer.BlockCopy(encryptedBytes, iv.Length, cipherText, 0, cipherText.Length);

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.IV = iv;

                using (var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                {
                    using (var msDecrypt = new MemoryStream(cipherText))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                string plainText = srDecrypt.ReadToEnd();
                                return plainText;
                            }
                        }
                    }
                }
            }
        }

        public string EncryptUserInfo(string plainText)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_configuration["EncryptionKey:key"]);
            using (var aesAlg = Aes.Create())
            {
                aesAlg.Key = keyBytes;
                aesAlg.GenerateIV();

                using (var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                {
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (var swEncrypt = new StreamWriter(csEncrypt))
                            {
                                swEncrypt.Write(plainText);
                            }
                        }

                        var iv = aesAlg.IV;
                        var encryptedContent = msEncrypt.ToArray();
                        var result = new byte[iv.Length + encryptedContent.Length];

                        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                        Buffer.BlockCopy(encryptedContent, 0, result, iv.Length, encryptedContent.Length);

                        return Convert.ToBase64String(result);
                    }
                }
            }
        }
    }
}


