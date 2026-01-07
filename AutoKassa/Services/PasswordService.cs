namespace AutoKassa.Services
{
    public class PasswordService : IPasswordService
    {
        /// <summary>
        /// Создать BCrypt хеш пароля
        /// </summary>
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt());
        }

        /// <summary>
        /// Проверить пароль против BCrypt хеша
        /// </summary>
        public bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}
