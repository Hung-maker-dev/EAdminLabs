using System;
using System.Security.Cryptography;
using System.Text;

namespace eAdmin.Web.Helpers
{
    /// <summary>
    /// Helper hash mật khẩu đơn giản dùng SHA-256 + salt.
    /// Production: Thay bằng BCrypt.Net hoặc ASP.NET Core Identity
    /// </summary>
    public static class PasswordHelper
    {
        private const string Salt = "eAdminLabSalt#2024";

        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + Salt);
            var hash  = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string hash)
            => HashPassword(password) == hash;
    }
}
