using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Services;
using System.Globalization;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel создания/редактирования кредитной карты
    /// </summary>
    public class CreditCardEditViewModel : ViewModelBase
    {
        private readonly ICreditCardService _creditCardService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;

        private CreditCard _card;
        private bool _isEditMode;

        private string _name = string.Empty;
        private string _bankName = string.Empty;
        private string _limitText = "0";
        private string _interestRateText = "";
        private string _statementDayText = "";
        private string _paymentDayText = "";
        private string _minimumPaymentPercentText = "5";
        private string _initialDebtText = "0";
        private bool _isActive = true;

        private bool _isSaving;

        public CreditCardEditViewModel(
            ICreditCardService creditCardService,
            IDialogService dialogService,
            IToastNotificationService toastService)
        {
            _creditCardService = creditCardService;
            _dialogService = dialogService;
            _toastService = toastService;

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => OnCancelled?.Invoke());
        }

        #region Свойства

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                    OnPropertyChanged(nameof(Title));
            }
        }

        public string Title => IsEditMode ? "Редактировать карту" : "Новая кредитная карта";

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                    ValidateName();
            }
        }

        public string BankName
        {
            get => _bankName;
            set => SetProperty(ref _bankName, value);
        }

        public string LimitText
        {
            get => _limitText;
            set
            {
                if (SetProperty(ref _limitText, value))
                    ValidateLimit();
            }
        }

        public string InterestRateText
        {
            get => _interestRateText;
            set
            {
                if (SetProperty(ref _interestRateText, value))
                    ValidateInterestRate();
            }
        }

        public string StatementDayText
        {
            get => _statementDayText;
            set
            {
                if (SetProperty(ref _statementDayText, value))
                    ValidateStatementDay();
            }
        }

        public string PaymentDayText
        {
            get => _paymentDayText;
            set
            {
                if (SetProperty(ref _paymentDayText, value))
                    ValidatePaymentDay();
            }
        }

        public string MinimumPaymentPercentText
        {
            get => _minimumPaymentPercentText;
            set
            {
                if (SetProperty(ref _minimumPaymentPercentText, value))
                    ValidateMinimumPaymentPercent();
            }
        }

        public string InitialDebtText
        {
            get => _initialDebtText;
            set
            {
                if (SetProperty(ref _initialDebtText, value))
                    ValidateInitialDebt();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string NameError => GetFirstError(nameof(Name));
        public string LimitError => GetFirstError(nameof(LimitText));
        public string InterestRateError => GetFirstError(nameof(InterestRateText));
        public string StatementDayError => GetFirstError(nameof(StatementDayText));
        public string PaymentDayError => GetFirstError(nameof(PaymentDayText));
        public string MinimumPaymentPercentError => GetFirstError(nameof(MinimumPaymentPercentText));
        public string InitialDebtError => GetFirstError(nameof(InitialDebtText));

        #endregion

        #region Команды

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region События

        public Func<Task>? OnSaved { get; set; }
        public Action? OnCancelled { get; set; }

        #endregion

        #region Методы

        public void InitializeForAdd()
        {
            IsEditMode = false;
            _card = new CreditCard();
            Name = string.Empty;
            BankName = string.Empty;
            LimitText = "0";
            InterestRateText = string.Empty;
            StatementDayText = string.Empty;
            PaymentDayText = string.Empty;
            MinimumPaymentPercentText = "5";
            InitialDebtText = "0";
            IsActive = true;
            ClearErrors();
            ValidateAll();
        }

        public void InitializeForEdit(CreditCard card)
        {
            IsEditMode = true;
            _card = card;
            Name = card.Name;
            BankName = card.BankName ?? string.Empty;
            LimitText = card.Limit.ToString(CultureInfo.InvariantCulture);
            InterestRateText = card.InterestRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            StatementDayText = card.StatementDay?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            PaymentDayText = card.PaymentDay?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            MinimumPaymentPercentText = card.MinimumPaymentPercent.ToString(CultureInfo.InvariantCulture);
            InitialDebtText = card.InitialDebt.ToString(CultureInfo.InvariantCulture);
            IsActive = card.IsActive;
            ClearErrors();
            ValidateAll();
        }

        private void ValidateAll()
        {
            ValidateName();
            ValidateLimit();
            ValidateInterestRate();
            ValidateStatementDay();
            ValidatePaymentDay();
            ValidateMinimumPaymentPercent();
            ValidateInitialDebt();
        }

        private void ValidateName()
        {
            if (string.IsNullOrWhiteSpace(_name))
                SetErrors(nameof(Name), new[] { "Введите название карты" });
            else
                ClearErrors(nameof(Name));
            OnPropertyChanged(nameof(NameError));
        }

        private void ValidateLimit()
        {
            if (!TryParsePositiveDecimal(_limitText, out _))
                SetErrors(nameof(LimitText), new[] { "Введите неотрицательную сумму" });
            else
                ClearErrors(nameof(LimitText));
            OnPropertyChanged(nameof(LimitError));
        }

        private void ValidateInterestRate()
        {
            if (!string.IsNullOrWhiteSpace(_interestRateText) && !TryParsePositiveDecimal(_interestRateText, out _))
                SetErrors(nameof(InterestRateText), new[] { "Введите неотрицательное число" });
            else
                ClearErrors(nameof(InterestRateText));
            OnPropertyChanged(nameof(InterestRateError));
        }

        private void ValidateDay(string text, string propertyName, Action notify)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearErrors(propertyName);
            }
            else if (!int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out int day) || day < 1 || day > 31)
            {
                SetErrors(propertyName, new[] { "День должен быть от 1 до 31" });
            }
            else
            {
                ClearErrors(propertyName);
            }
            notify();
        }

        private void ValidateStatementDay()
        {
            ValidateDay(_statementDayText, nameof(StatementDayText), () => OnPropertyChanged(nameof(StatementDayError)));
        }

        private void ValidatePaymentDay()
        {
            ValidateDay(_paymentDayText, nameof(PaymentDayText), () => OnPropertyChanged(nameof(PaymentDayError)));
        }

        private void ValidateMinimumPaymentPercent()
        {
            if (!TryParsePositiveDecimal(_minimumPaymentPercentText, out decimal percent) || percent < 0 || percent > 100)
                SetErrors(nameof(MinimumPaymentPercentText), new[] { "Введите процент от 0 до 100" });
            else
                ClearErrors(nameof(MinimumPaymentPercentText));
            OnPropertyChanged(nameof(MinimumPaymentPercentError));
        }

        private void ValidateInitialDebt()
        {
            if (!TryParsePositiveDecimal(_initialDebtText, out _))
                SetErrors(nameof(InitialDebtText), new[] { "Введите неотрицательную сумму" });
            else
                ClearErrors(nameof(InitialDebtText));
            OnPropertyChanged(nameof(InitialDebtError));
        }

        private static bool TryParsePositiveDecimal(string text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = text.Replace(',', '.');
            if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                return false;

            value = parsed;
            return parsed >= 0;
        }

        private bool CanSave() => !_isSaving && !HasErrors;

        private async Task SaveAsync()
        {
            if (_isSaving) return;

            ValidateAll();
            if (HasErrors) return;

            _isSaving = true;
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                TryParsePositiveDecimal(_limitText, out decimal limit);
                TryParsePositiveDecimal(_initialDebtText, out decimal initialDebt);
                TryParsePositiveDecimal(_minimumPaymentPercentText, out decimal minimumPaymentPercent);

                decimal? interestRate = string.IsNullOrWhiteSpace(_interestRateText)
                    ? null
                    : (TryParsePositiveDecimal(_interestRateText, out decimal ir) ? ir : null);

                int? statementDay = string.IsNullOrWhiteSpace(_statementDayText)
                    ? null
                    : (int.TryParse(_statementDayText, NumberStyles.Any, CultureInfo.InvariantCulture, out int sd) ? sd : null);

                int? paymentDay = string.IsNullOrWhiteSpace(_paymentDayText)
                    ? null
                    : (int.TryParse(_paymentDayText, NumberStyles.Any, CultureInfo.InvariantCulture, out int pd) ? pd : null);

                if (IsEditMode)
                {
                    _card.Name = _name.Trim();
                    _card.BankName = string.IsNullOrWhiteSpace(_bankName) ? null : _bankName.Trim();
                    _card.Limit = limit;
                    _card.InterestRate = interestRate;
                    _card.StatementDay = statementDay;
                    _card.PaymentDay = paymentDay;
                    _card.MinimumPaymentPercent = minimumPaymentPercent;
                    _card.InitialDebt = initialDebt;
                    _card.IsActive = _isActive;

                    await _creditCardService.UpdateAsync(_card);
                    _toastService.ShowSuccess("Карта сохранена");
                }
                else
                {
                    var card = new CreditCard
                    {
                        Name = _name.Trim(),
                        BankName = string.IsNullOrWhiteSpace(_bankName) ? null : _bankName.Trim(),
                        Limit = limit,
                        InterestRate = interestRate,
                        StatementDay = statementDay,
                        PaymentDay = paymentDay,
                        MinimumPaymentPercent = minimumPaymentPercent,
                        InitialDebt = initialDebt,
                        IsActive = _isActive
                    };

                    await _creditCardService.CreateAsync(card);
                    _toastService.ShowSuccess("Карта добавлена");
                }

                if (OnSaved != null)
                    await OnSaved();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        #endregion
    }
}
