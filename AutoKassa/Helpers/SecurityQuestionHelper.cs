using System.Collections.Generic;
using AutoKassa.Models.Enums;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Вспомогательный класс для работы с секретными вопросами
    /// </summary>
    public static class SecurityQuestionHelper
    {
        /// <summary>
        /// Получить текст секретного вопроса
        /// </summary>
        public static string GetQuestionText(SecurityQuestion question)
        {
            return question switch
            {
                SecurityQuestion.MotherMaidenName => "Девичья фамилия матери",
                SecurityQuestion.FirstPetName => "Кличка первого домашнего животного",
                SecurityQuestion.FirstSchoolName => "Название первой школы",
                SecurityQuestion.FavoriteDish => "Любимое блюдо",
                SecurityQuestion.CityOfBirth => "Город рождения",
                SecurityQuestion.Custom => "Свой вопрос",
                _ => "Неизвестный вопрос"
            };
        }

        /// <summary>
        /// Получить список всех секретных вопросов для выбора
        /// </summary>
        public static List<SecurityQuestionItem> GetQuestionsList()
        {
            return new List<SecurityQuestionItem>
            {
                new SecurityQuestionItem { Question = SecurityQuestion.MotherMaidenName, Text = GetQuestionText(SecurityQuestion.MotherMaidenName) },
                new SecurityQuestionItem { Question = SecurityQuestion.FirstPetName, Text = GetQuestionText(SecurityQuestion.FirstPetName) },
                new SecurityQuestionItem { Question = SecurityQuestion.FirstSchoolName, Text = GetQuestionText(SecurityQuestion.FirstSchoolName) },
                new SecurityQuestionItem { Question = SecurityQuestion.FavoriteDish, Text = GetQuestionText(SecurityQuestion.FavoriteDish) },
                new SecurityQuestionItem { Question = SecurityQuestion.CityOfBirth, Text = GetQuestionText(SecurityQuestion.CityOfBirth) },
                new SecurityQuestionItem { Question = SecurityQuestion.Custom, Text = GetQuestionText(SecurityQuestion.Custom) }
            };
        }
    }

    /// <summary>
    /// Элемент списка секретных вопросов
    /// </summary>
    public class SecurityQuestionItem
    {
        public SecurityQuestion Question { get; set; }
        public string Text { get; set; }
    }
}