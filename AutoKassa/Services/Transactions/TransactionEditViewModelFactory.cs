using AutoKassa.Models;
using AutoKassa.Services;
using AutoKassa.ViewModels;

namespace AutoKassa.Services.Transactions
{
    /// <summary>
    /// Реализация фабрики для создания <see cref="TransactionEditViewModel"/>.
    /// </summary>
    public class TransactionEditViewModelFactory : ITransactionEditViewModelFactory
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ICreditCardService _creditCardService;
        private readonly ICounterpartyService _counterpartyService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;
        private readonly IToastNotificationService _toastService;

        /// <summary>
        /// Создаёт экземпляр фабрики с инжекцией всех зависимостей ViewModel операции.
        /// </summary>
        public TransactionEditViewModelFactory(
            ITransactionService transactionService,
            ICategoryService categoryService,
            ICreditCardService creditCardService,
            ICounterpartyService counterpartyService,
            IDialogService dialogService,
            ISettingsService settingsService,
            IToastNotificationService toastService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _creditCardService = creditCardService;
            _counterpartyService = counterpartyService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _toastService = toastService;
        }

        /// <inheritdoc />
        public TransactionEditViewModel CreateForAdd()
        {
            var vm = new TransactionEditViewModel(
                _transactionService,
                _categoryService,
                _creditCardService,
                _counterpartyService,
                _dialogService,
                _settingsService,
                _toastService);

            vm.InitializeForAdd();
            return vm;
        }

        /// <inheritdoc />
        public TransactionEditViewModel CreateForEdit(Transaction transaction)
        {
            var vm = new TransactionEditViewModel(
                _transactionService,
                _categoryService,
                _creditCardService,
                _counterpartyService,
                _dialogService,
                _settingsService,
                _toastService);

            vm.InitializeForEdit(transaction);
            return vm;
        }
    }
}
