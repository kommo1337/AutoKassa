namespace AutoKassa.Models.Enums
{
    public enum SecurityQuestion
    {
        /// <summary>
        /// Девичья фамилия матери
        /// </summary>
        MotherMaidenName = 1,

        /// <summary>
        /// Кличка первого домашнего животного
        /// </summary>
        FirstPetName = 2,

        /// <summary>
        /// Название первой школы
        /// </summary>
        FirstSchoolName = 3,

        /// <summary>
        /// Любимое блюдо
        /// </summary>
        FavoriteDish = 4,

        /// <summary>
        /// Город рождения
        /// </summary>
        CityOfBirth = 5,

        /// <summary>
        /// Свой вопрос (текст хранится в CustomSecurityQuestion)
        /// </summary>
        Custom = 6
    }
}
