namespace AutoKassa.Services
{
    public interface IPasswordService
    {
        /// <summary>
        /// Создать хеш пароля
        /// </summary>
        string HashPassword(string password);

        /// <summary>
        /// Проверить пароль против хеша
        /// </summary>
        bool VerifyPassword(string password, string hash);
    }
}
