using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Обёртка над Transaction с поддержкой выделения для массового удаления
    /// </summary>
    public class SelectableTransaction : ViewModelBase
    {
        private bool _isSelected;

        public Transaction Transaction { get; }
        public Action? SelectionChanged { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                    SelectionChanged?.Invoke();
            }
        }

        public SelectableTransaction(Transaction transaction)
        {
            Transaction = transaction;
        }
    }

    /// <summary>
    /// Группа операций за один день с поддержкой выделения и инлайн-добавления
    /// </summary>
    public class SelectableDateGroup : ViewModelBase
    {
        private bool _isInlineOpen;
        private OperationType _inlineType = OperationType.Expense;
        private PaymentType _inlinePaymentType = PaymentType.Cash;
        private string _inlineAmountText = string.Empty;
        private Category? _inlineCategory;
        private string _inlineDescription = string.Empty;
        private ObservableCollection<Category> _allCategories = new();
        private bool _isSaving;

        // ── Основные данные группы ──────────────────────────────────────────

        public DateTime Date { get; set; }
        public decimal DayTotal { get; set; }
        public ObservableCollection<SelectableTransaction> Items { get; set; } = new();

        public string DateLabel => Date == DateTime.Today
            ? "Сегодня"
            : Date == DateTime.Today.AddDays(-1)
                ? "Вчера"
                : Date.ToString("d MMMM", new CultureInfo("ru-RU"));

        public string DayTotalFormatted => DayTotal >= 0
            ? $"+{DayTotal:N0} ₽"
            : $"{DayTotal:N0} ₽";

        public string DayTotalColor => DayTotal >= 0 ? "#22c55e" : "#ef4444";

        // ── Инлайн-состояние ────────────────────────────────────────────────

        public bool IsInlineOpen
        {
            get => _isInlineOpen;
            set => SetProperty(ref _isInlineOpen, value);
        }

        public OperationType InlineType
        {
            get => _inlineType;
            set
            {
                if (SetProperty(ref _inlineType, value))
                {
                    OnPropertyChanged(nameof(IsInlineIncome));
                    OnPropertyChanged(nameof(GroupInlineCategories));
                    GroupInlineCategory = GroupInlineCategories.FirstOrDefault();
                }
            }
        }

        public PaymentType InlinePaymentType
        {
            get => _inlinePaymentType;
            set
            {
                if (SetProperty(ref _inlinePaymentType, value))
                {
                    OnPropertyChanged(nameof(InlineIsCash));
                    OnPropertyChanged(nameof(InlineIsNonCash));
                }
            }
        }

        public string InlineAmountText
        {
            get => _inlineAmountText;
            set => SetProperty(ref _inlineAmountText, value);
        }

        public Category? GroupInlineCategory
        {
            get => _inlineCategory;
            set => SetProperty(ref _inlineCategory, value);
        }

        public string InlineDescription
        {
            get => _inlineDescription;
            set => SetProperty(ref _inlineDescription, value);
        }

        public bool IsInlineIncome => _inlineType == OperationType.Income;
        public bool InlineIsCash    => _inlinePaymentType == PaymentType.Cash;
        public bool InlineIsNonCash => _inlinePaymentType == PaymentType.NonCash;

        public IEnumerable<Category> GroupInlineCategories =>
            _allCategories.Where(c => c.Id > 0 && c.Type == _inlineType);

        // ── Команды ─────────────────────────────────────────────────────────

        public ICommand? OpenGroupInlineCommand              { get; private set; }
        public ICommand? GroupInlineToggleTypeCommand        { get; private set; }
        public ICommand? GroupInlineSelectExpenseCommand     { get; private set; }
        public ICommand? GroupInlineSelectIncomeCommand      { get; private set; }
        public ICommand? GroupInlineTogglePaymentCommand     { get; private set; }
        public ICommand? GroupInlineSaveCommand              { get; private set; }
        public ICommand? GroupInlineCancelCommand            { get; private set; }

        /// <summary>
        /// Вызывается из ViewModel после создания группы — инициализирует команды
        /// и привязывает категории + делегат сохранения.
        /// </summary>
        public void InitInline(ObservableCollection<Category> categories,
                               Func<SelectableDateGroup, Task> onSave,
                               IToastNotificationService? toastService = null)
        {
            _allCategories = categories;
            GroupInlineCategory = GroupInlineCategories.FirstOrDefault();

            OpenGroupInlineCommand = new RelayCommand(_ =>
            {
                IsInlineOpen = !IsInlineOpen;
                if (!IsInlineOpen) ResetInline();
            });

            GroupInlineToggleTypeCommand = new RelayCommand(_ =>
                InlineType = InlineType == OperationType.Expense
                    ? OperationType.Income
                    : OperationType.Expense);

            GroupInlineSelectExpenseCommand = new RelayCommand(_ => InlineType = OperationType.Expense);
            GroupInlineSelectIncomeCommand  = new RelayCommand(_ => InlineType = OperationType.Income);

            GroupInlineTogglePaymentCommand = new RelayCommand(_ =>
                InlinePaymentType = InlinePaymentType == PaymentType.Cash
                    ? PaymentType.NonCash
                    : PaymentType.Cash);

            GroupInlineSaveCommand = new RelayCommand(async _ =>
            {
                if (_isSaving) return;
                _isSaving = true;
                (GroupInlineSaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                try
                {
                    await onSave(this);
                }
                finally
                {
                    _isSaving = false;
                    (GroupInlineSaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }, _ => !_isSaving);
            GroupInlineCancelCommand = new RelayCommand(_ =>
            {
                var hasInput = !string.IsNullOrWhiteSpace(InlineAmountText)
                               || !string.IsNullOrWhiteSpace(InlineDescription);

                if (!hasInput || toastService == null)
                {
                    IsInlineOpen = false;
                    ResetInline();
                    return;
                }

                toastService.ShowWithAction(
                    "Отменить добавление? Данные будут потеряны.",
                    "Отменить",
                    () => { IsInlineOpen = false; ResetInline(); },
                    ToastType.Info);
            });
        }

        private void ResetInline()
        {
            InlineType        = OperationType.Expense;
            InlinePaymentType = PaymentType.Cash;
            InlineAmountText  = string.Empty;
            InlineDescription = string.Empty;
            GroupInlineCategory = GroupInlineCategories.FirstOrDefault();
        }
    }
}
