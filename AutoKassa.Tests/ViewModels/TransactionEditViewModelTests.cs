using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using AutoKassa.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoKassa.Tests.ViewModels
{
    /// <summary>
    /// Регрессионные тесты на «фантомные операции»: операция с будущей датой
    /// раньше сохранялась молча и «появлялась из ниоткуда» в свой день.
    /// Теперь сохранение требует подтверждения через диалог.
    /// </summary>
    public class TransactionEditViewModelTests : IDisposable
    {
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly AppDbContext _ctx;
        private readonly FakeDialogService _dialogService;
        private readonly FakeToastNotificationService _toastService;
        private readonly Category _category;

        public TransactionEditViewModelTests()
        {
            (_factory, _conn) = TestDatabase.CreateWithFactory();
            _ctx = _factory.CreateDbContext();
            _dialogService = new FakeDialogService();
            _toastService = new FakeToastNotificationService();
            _category = TestDatabase.SeedExpenseCategory(_ctx);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        private TransactionEditViewModel CreateViewModel()
        {
            var cache = new ReferenceDataCache(_factory, new DataChangeService());
            return new TransactionEditViewModel(
                new TransactionService(_factory),
                new CategoryService(_factory, cache),
                new CreditCardService(_factory, cache),
                new CounterpartyService(_factory, cache),
                _dialogService,
                new SettingsService(_factory),
                _toastService);
        }

        private async Task<TransactionEditViewModel> CreateInitializedViewModelAsync()
        {
            var vm = CreateViewModel();
            await TestWaitHelper.WaitUntilAsync(() => vm.Categories.Count > 0);

            vm.Type = OperationType.Expense;
            vm.AmountText = "100";
            vm.SelectedCategory = _category;
            return vm;
        }

        [Fact]
        public async Task SaveAsync_FutureDate_ConfirmationDeclined_DoesNotSave()
        {
            var vm = await CreateInitializedViewModelAsync();
            vm.Date = DateTime.Today.AddDays(5);
            _dialogService.ConfirmationResult = false;

            vm.SaveCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => _dialogService.LastConfirmation != null);
            await Task.Delay(300); // даём время ошибочно продолжить сохранение

            vm.SavedTransaction.Should().BeNull();
            (await _ctx.Transactions.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_FutureDate_ConfirmationAccepted_Saves()
        {
            var vm = await CreateInitializedViewModelAsync();
            var futureDate = DateTime.Today.AddDays(5);
            vm.Date = futureDate;
            _dialogService.ConfirmationResult = true;

            vm.SaveCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.SavedTransaction != null);

            _dialogService.LastConfirmation.Should().NotBeNull();
            var saved = await _ctx.Transactions.SingleAsync();
            saved.Date.Should().Be(futureDate);
            saved.Amount.Should().Be(100m);
        }

        [Fact]
        public async Task SaveAsync_Today_SavesWithoutConfirmation()
        {
            var vm = await CreateInitializedViewModelAsync();
            vm.Date = DateTime.Today;

            vm.SaveCommand.Execute(null);
            await TestWaitHelper.WaitUntilAsync(() => vm.SavedTransaction != null);

            _dialogService.LastConfirmation.Should().BeNull();
            (await _ctx.Transactions.CountAsync()).Should().Be(1);
        }
    }
}
