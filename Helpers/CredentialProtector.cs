using System;
using System.Security.Cryptography;
using System.Text;

namespace AutoRegressionVM.Helpers
{
    /// <summary>
    /// DPAPI를 사용한 자격 증명 암호화/복호화
    /// 현재 Windows 사용자 컨텍스트에서만 복호화 가능
    /// </summary>
    public static class CredentialProtector
    {
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                // DPAPI 사용 불가 시 원본 반환 (호환성)
                return plainText;
            }
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (FormatException)
            {
                // Base64가 아니면 평문으로 간주 (기존 설정 마이그레이션)
                return encryptedText;
            }
            catch (CryptographicException)
            {
                // 다른 사용자가 암호화한 경우 등
                return encryptedText;
            }
        }

        /// <summary>
        /// 문자열이 DPAPI로 암호화되었는지 간단히 판별
        /// </summary>
        public static bool IsEncrypted(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                var bytes = Convert.FromBase64String(text);
                // DPAPI 암호화된 데이터는 최소 수십 바이트
                return bytes.Length > 20;
            }
            catch
            {
                return false;
            }
        }
    }
}
