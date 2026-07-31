using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel формы погашения кредитной карты
    /// </summary>
    public class CreditCardRepaymentViewModel : ViewModelBase
    {
        private readonly ICreditCardService _creditCardService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly IDataChangeService _dataChangeService;

        private CreditCard _card = null!;
        private decimal _currentDebt;
        private string _amountText = "";
        private decimal _amount;
        private PaymentType _paymentType = PaymentType.Cash;
        private DateTime _date;
        private string? _description;
        private bool _isSaving;

        public CreditCardRepaymentViewModel(
            ICreditCardService creditCardService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            IDataChangeService dataChangeService)
        {
            _creditCardService = creditCardService;
            _dialogService = dialogService;
            _toastService = toastService;
            _dataChangeService = dataChangeService;

            _date = DateTime.Now;

            RepayCommand = new RelayCommand(async _ => await RepayAsync(), _ => CanRepay());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        #region Properties

        /// <summary>
        /// Кредитная карта, которую погашают
        /// </summary>
        public CreditCard Card
        {
            get => _card;
            set
            {
                if (SetProperty(ref _card, value))
                {
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(CurrentDebtFormatted));
                }
            }
        }

        /// <summary>
        /// Текущий долг по карте
        /// </summary>
        public decimal CurrentDebt
        {
            get => _currentDebt;
            set
            {
                if (SetProperty(ref _currentDebt, value))
                {
                    OnPropertyChanged(nameof(CurrentDebtFormatted));
                    ValidateAmount();
                }
            }
        }

        public string CurrentDebtFormatted => $"{_currentDebt:N2} ₽";

        public string Title => $"Погашение: {Card?.Name}";

        public string AmountText
        {
            get => _amountText;
            set
            {
                if (SetProperty(ref _amountText, value))
                {
                    decimal.TryParse(
                        value,
                        NumberStyles.Number,
                        CultureInfo.CurrentCulture,
                        out _amount);

                    ValidateAmount();
                }
            }
        }

        public decimal Amount => _amount;

        public PaymentType PaymentType
        {
            get => _paymentType;
            set => SetProperty(ref _paymentType, value);
        }

        public DateTime Date
        {
            get => _date;
            set
            {
                if (SetProperty(ref _date, value))
                    ValidateDate();
            }
        }

        public string? Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                    ValidateDescription();
            }
        }

        public string AmountError => GetFirstError(nameof(Amount));

        public string DateError => GetFirstError(nameof(Date));

        public string DescriptionError => GetFirstError(nameof(Description));

        public ICommand RepayCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Созданная операция-погашение
        /// </summary>
        public Transaction? SavedTransaction { get; private set; }

        /// <summary>
        /// Вызывается после успешного создания погашения
        /// </summary>
        public Action? OnSaved { get; set; }

        /// <summary>
        /// Вызывается при отмене формы
        /// </summary>
        public Action? OnCancelled { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Инициализировать форму для погашения указанной карты
        /// </summary>
        public async Task InitializeAsync(CreditCard card)
        {
            Card = card;
            CurrentDebt = await _creditCardService.GetCurrentDebtAsync(card.Id);
            _date = DateTime.Now;
            PaymentType = PaymentType.Cash;
            Description = null;
            _isSaving = false;
            SavedTransaction = null;
            ClearErrors(nameof(Amount));
            ClearErrors(nameof(Date));
            ClearErrors(nameof(Description));
            AmountText = CurrentDebt > 0 ? CurrentDebt.ToString("N2", CultureInfo.CurrentCulture) : "";
        }

        private void Cancel()
        {
            OnCancelled?.Invoke();
        }

        private void ValidateAmount()
        {
            if (_amount <= 0)
                SetErrors(nameof(Amount), new[] { "Сумма должна быть больше 0" });
            else if (_amount > _currentDebt)
                SetErrors(nameof(Amount), new[] { $"Сумма не может превышать текущий долг ({_currentDebt:N2})" });
            else
                ClearErrors(nameof(Amount));

            OnPropertyChanged(nameof(AmountError));
            (RepayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ValidateDate()
        {
            if (_date > DateTime.Now)
                SetErrors(nameof(Date), new[] { "Дата погашения не может быть в будущем" });
            else
                ClearErrors(nameof(Date));

            OnPropertyChanged(nameof(DateError));
            (RepayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ValidateDescription()
        {
            // Соответствует [MaxLength(500)] в Models/Transaction.cs
            const int MaxDescriptionLength = 500;

            if ((_description ?? string.Empty).Length > MaxDescriptionLength)
                SetErrors(nameof(Description), new[] { $"Описание не должно превышать {MaxDescriptionLength} символов" });
            else
                ClearErrors(nameof(Description));

            OnPropertyChanged(nameof(DescriptionError));
            (RepayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanRepay() => !_isSaving && !HasErrors && !string.IsNullOrEmpty(_amountText);

        private async Task RepayAsync()
        {
            if (_isSaving) return;

            ValidateAmount();
            ValidateDate();
            ValidateDescription();
            if (HasErrors) return;

            _isSaving = true;
            (RepayCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                SavedTransaction = await _creditCardService.RepayDebtAsync(
                    _card.Id,
                    _amount,
                    _paymentType,
                    _date,
                    _description,
                    default);

                _toastService.ShowSuccess("Погашение создано");
                _dataChangeService.NotifyDataChanged();
                OnSaved?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка погашения: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
                (RepayCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        #endregion
    }
}
