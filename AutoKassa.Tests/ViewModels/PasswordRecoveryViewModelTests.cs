using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    public class PasswordRecoveryViewModelTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly SettingsService _settingsService;
        private readonly PasswordService _passwordService;
        private readonly FakeDialogService _dialogService;

        public PasswordRecoveryViewModelTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _settingsService = new SettingsService(_factory);
            _passwordService = new PasswordService();
            _dialogService = new FakeDialogService();
        }

        public void Dispose()
        {
            _conn.Dispose();
        }

        /// <summary>
        /// Задать пароль с секретным вопросом. Ответ хешируется в нормализованном виде
        /// (Trim().ToLower()) — так же, как VerifyAnswerAsync нормализует ввод пользователя.
        /// </summary>
        private async Task SeedPasswordWithQuestion(SecurityQuestion question, string answer = "ответ", string? customQuestion = null)
        {
            await _settingsService.SetPasswordAsync(
                _passwordService.HashPassword("oldpass1"),
                question,
                _passwordService.HashPassword(answer.Trim().ToLower()),
                customQuestion);
        }

        private PasswordRecoveryViewModel CreateViewModel() =>
            new PasswordRecoveryViewModel(_passwordService, _settingsService, _dialogService);

        [Fact]
        public async Task Constructor_LoadsPredefinedQuestionText()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName);

            var vm = CreateViewModel();

            vm.QuestionText.Should().Be(SecurityQuestionHelper.GetQuestionText(SecurityQuestion.MotherMaidenName));
        }

        [Fact]
        public void Constructor_NoQuestion_ShowsPlaceholder()
        {
            var vm = CreateViewModel();

            vm.QuestionText.Should().Be("Секретный вопрос не установлен");
        }

        [Fact]
        public async Task Constructor_CustomQuestion_UsesCustomText()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.Custom, customQuestion: "Мой личный вопрос?");

            var vm = CreateViewModel();

            vm.QuestionText.Should().Be("Мой личный вопрос?");
        }

        [Fact]
        public async Task VerifyAnswer_WrongAnswer_SetsErrorAndClearsAnswer()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName);
            var vm = CreateViewModel();
            vm.Answer = "неправильный";

            vm.VerifyAnswerCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.ErrorMessage != null || vm.Answer != "неправильный");

            vm.IsAnswerVerified.Should().BeFalse();
            vm.Answer.Should().BeEmpty();
            vm.ErrorMessage.Should().Contain("Неверный ответ");
        }

        [Fact]
        public async Task VerifyAnswer_CorrectAnswer_NormalizesCaseAndSpaces()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName, answer: "ответ");
            var vm = CreateViewModel();
            vm.Answer = "  ОТВЕТ  ";

            vm.VerifyAnswerCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.IsAnswerVerified);

            vm.IsAnswerVerified.Should().BeTrue();
            vm.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task VerifyAnswer_EmptyAnswer_NotAllowed()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName);
            var vm = CreateViewModel();

            vm.VerifyAnswerCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public async Task ResetPassword_TooShort_SetsError()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName);
            var vm = CreateViewModel();
            vm.Answer = "ответ";
            vm.VerifyAnswerCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.IsAnswerVerified);

            vm.NewPassword = "123";
            vm.ConfirmNewPassword = "123";
            vm.ResetPasswordCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public async Task ResetPassword_Mismatch_NotAllowed()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName);
            var vm = CreateViewModel();
            vm.Answer = "ответ";
            vm.VerifyAnswerCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.IsAnswerVerified);

            vm.NewPassword = "newpass123";
            vm.ConfirmNewPassword = "newpass456";
            vm.ResetPasswordCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public async Task ResetPassword_Success_UpdatesPasswordAndNotifies()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.MotherMaidenName);
            var vm = CreateViewModel();
            var resetNotified = false;
            vm.OnPasswordReset = () => resetNotified = true;

            vm.Answer = "ответ";
            vm.VerifyAnswerCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.IsAnswerVerified);

            vm.NewPassword = "newpass123";
            vm.ConfirmNewPassword = "newpass123";
            vm.ResetPasswordCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialogService.LastInfo != null);

            _dialogService.LastInfo.Should().Contain("Пароль успешно изменен");
            resetNotified.Should().BeTrue();
            _passwordService.VerifyPassword("newpass123", _settingsService.GetSettings().PasswordHash).Should().BeTrue();
        }

        [Fact]
        public async Task Reset_ClearsStateAndReloadsQuestion()
        {
            await SeedPasswordWithQuestion(SecurityQuestion.FirstPetName);
            var vm = CreateViewModel();
            vm.Answer = "ответ";
            vm.VerifyAnswerCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.IsAnswerVerified);
            vm.NewPassword = "newpass123";
            vm.ConfirmNewPassword = "newpass123";

            vm.Reset();

            vm.Answer.Should().BeEmpty();
            vm.NewPassword.Should().BeEmpty();
            vm.ConfirmNewPassword.Should().BeEmpty();
            vm.ErrorMessage.Should().BeNull();
            vm.IsAnswerVerified.Should().BeFalse();
            vm.QuestionText.Should().Be(SecurityQuestionHelper.GetQuestionText(SecurityQuestion.FirstPetName));
        }
    }
}
