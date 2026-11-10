using AutoKassa.Tests.Infrastructure;
using AutoKassa.Tests.Services.Backup.YandexDisk;
using AutoKassa.ViewModels;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    /// <summary>
    /// Тесты диалога ввода ClientId Яндекс Диска: валидация, сохранение через
    /// IYandexDiskClientIdStore, результат закрытия диалога.
    /// </summary>
    public class YandexClientIdDialogViewModelTests
    {
        private readonly FakeYandexDiskClientIdStore _store;
        private readonly YandexClientIdDialogViewModel _vm;

        public YandexClientIdDialogViewModelTests()
        {
            _store = new FakeYandexDiskClientIdStore();
            _vm = new YandexClientIdDialogViewModel(_store);
        }

        [Fact]
        public void EmptyClientId_IsInvalidAndShowsError()
        {
            _vm.ClientId = "   ";

            _vm.ClientIdError.Should().NotBeNullOrEmpty();
            _vm.SaveCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void ValidClientId_IsSavable()
        {
            _vm.ClientId = "client-id-123";

            _vm.ClientIdError.Should().BeNull();
            _vm.SaveCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public async Task SaveCommand_TrimsAndStoresValue_ClosesWithSuccess()
        {
            bool? closeResult = null;
            _vm.OnClose = result => closeResult = result;
            _vm.ClientId = "  client-id-abc  ";

            _vm.SaveCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => closeResult != null);

            _store.SaveCallCount.Should().Be(1);
            _store.Stored.Should().Be("client-id-abc");
            closeResult.Should().BeTrue();
        }

        [Fact]
        public async Task SaveCommand_WhenStoreThrowsBusinessError_ShowsErrorAndStaysOpen()
        {
            _store.SaveException = new InvalidOperationException("Введите ClientId приложения Яндекс Диска.");
            _vm.ClientId = "x";
            var closed = false;
            _vm.OnClose = _ => closed = true;

            _vm.SaveCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _vm.ClientIdError != null);

            _vm.ClientIdError.Should().Contain("ClientId");
            closed.Should().BeFalse("диалог остаётся открытым для исправления");
        }

        [Fact]
        public void CancelCommand_ClosesWithoutSaving()
        {
            bool? closeResult = null;
            _vm.OnClose = result => closeResult = result;
            _vm.ClientId = "client-id-abc";

            _vm.CancelCommand.Execute(null);

            _store.SaveCallCount.Should().Be(0);
            closeResult.Should().BeFalse();
        }
    }
}
