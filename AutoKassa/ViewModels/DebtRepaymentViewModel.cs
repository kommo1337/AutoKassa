using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel формы погашения долга
    /// </summary>
    public class DebtRepaymentViewModel : ViewModelBase
    {
        private readonly IDebtService _debtService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly IDataChangeService _dataChangeService;

        private DebtItem _debt = null!;
        private string _amountText = "";
        private decimal _amount;
        private PaymentType _paymentType = PaymentType.Cash;
        private DateTime _date;
        private string? _description;
        private bool _isSaving;

        public DebtRepaymentViewModel(
            IDebtService debtService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            IDataChangeService dataChangeService)
        {
            _debtService = debtService;
            _dialogService = dialogService;
            _toastService = toastService;
            _dataChangeService = dataChangeService;

            _date = DateTime.Now;

            RepayCommand = new RelayCommand(async _ => await RepayAsync(), _ => CanRepay());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        #region Properties

        public DebtItem Debt
        {
            get => _debt;
            set
            {
                if (SetProperty(ref _debt, value))
                    OnPropertyChanged(nameof(Title));
            }
        }

        public string Title => $"Погашение долга: {Debt.CounterpartyName}";

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
            set => SetProperty(ref _description, value);
        }

        public string AmountError => GetFirstError(nameof(Amount));

        public string DateError => GetFirstError(nameof(Date));

        public ICommand RepayCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Созданная операция-погашение (для оптимистичного обновления UI).
        /// </summary>
        public Transaction? SavedTransaction { get; private set; }

        /// <summary>
        /// Вызывается после успешного создания погашения.
        /// </summary>
        public Action? OnSaved { get; set; }

        /// <summary>
        /// Вызывается при отмене формы.
        /// </summary>
        public Action? OnCancelled { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Инициализировать форму для погашения указанного долга.
        /// </summary>
        public void Initialize(DebtItem debt)
        {
            Debt = debt;
            _date = DateTime.Now;
            PaymentType = PaymentType.Cash;
            Description = null;
            _isSaving = false;
            SavedTransaction = null;
            ClearErrors(nameof(Amount));
            ClearErrors(nameof(Date));
            AmountText = debt.RemainingAmount.ToString("N2", CultureInfo.CurrentCulture);
        }

        private void Cancel()
        {
            OnCancelled?.Invoke();
        }

        private void ValidateAmount()
        {
            if (_amount <= 0)
                SetErrors(nameof(Amount), new[] { "Сумма должна быть больше 0" });
            else if (_amount > _debt.RemainingAmount)
                SetErrors(nameof(Amount), new[] { $"Сумма не может превышать остаток ({_debt.RemainingAmount:N2})" });
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

        private bool CanRepay() => !_isSaving && !HasErrors && !string.IsNullOrEmpty(_amountText);

        private async Task RepayAsync()
        {
            if (_isSaving) return;

            ValidateAmount();
            ValidateDate();
            if (HasErrors) return;

            _isSaving = true;
            (RepayCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                SavedTransaction = await _debtService.RepayAsync(
                    _debt.TransactionId,
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
