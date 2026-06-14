using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Элемент списка кредитных карт с рассчитанными метриками
    /// </summary>
    public class CreditCardListItemViewModel : ViewModelBase
    {
        public CreditCard Card { get; }

        private decimal _currentDebt;
        private decimal _availableLimit;
        private decimal _minimumPayment;
        private DateTime? _nextPaymentDate;

        public CreditCardListItemViewModel(CreditCard card)
        {
            Card = card;
        }

        public decimal CurrentDebt
        {
            get => _currentDebt;
            set { if (SetProperty(ref _currentDebt, value)) OnPropertyChanged(nameof(CurrentDebtFormatted)); }
        }

        public string CurrentDebtFormatted => $"{_currentDebt:N0} ₽";

        public decimal AvailableLimit
        {
            get => _availableLimit;
            set { if (SetProperty(ref _availableLimit, value)) OnPropertyChanged(nameof(AvailableLimitFormatted)); }
        }

        public string AvailableLimitFormatted => $"{_availableLimit:N0} ₽";

        public decimal MinimumPayment
        {
            get => _minimumPayment;
            set { if (SetProperty(ref _minimumPayment, value)) OnPropertyChanged(nameof(MinimumPaymentFormatted)); }
        }

        public string MinimumPaymentFormatted => $"{_minimumPayment:N0} ₽";

        public string InterestRateFormatted => $"{Card.InterestRate:N1}%";

        public DateTime? NextPaymentDate
        {
            get => _nextPaymentDate;
            set { if (SetProperty(ref _nextPaymentDate, value)) OnPropertyChanged(nameof(NextPaymentDateFormatted)); }
        }

        private static readonly CultureInfo RuCulture = new("ru-RU");
        public string NextPaymentDateFormatted =>
            _nextPaymentDate.HasValue ? _nextPaymentDate.Value.ToString("d MMMM", RuCulture) : "—";
    }

    /// <summary>
    /// ViewModel экрана кредитных карт
    /// </summary>
    public class CreditCardsViewModel : ViewModelBase, INavigationAware
    {
        private readonly ICreditCardService _creditCardService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly IDataChangeService _dataChangeService;

        private bool _isInitialized;
        private bool _needsRefresh;

        private ObservableCollection<CreditCardListItemViewModel> _creditCardItems;
        private CreditCardListItemViewModel _selectedCardItem;
        private bool _isLoading;
        private bool _isModalOpen;
        private CreditCardEditViewModel _editViewModel;

        private decimal _totalLimit;
        private decimal _totalDebt;
        private decimal _totalAvailable;
        private DateTime? _nearestPaymentDate;
        private decimal _nearestPaymentAmount;
        private ObservableCollection<CreditCardPurchase> _selectedCardPurchases;

        private static readonly CultureInfo RuCulture = new("ru-RU");

        public CreditCardsViewModel(
            ICreditCardService creditCardService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            IDataChangeService dataChangeService)
        {
            _creditCardService = creditCardService;
            _dialogService = dialogService;
            _toastService = toastService;
            _dataChangeService = dataChangeService;

            CreditCardItems = new ObservableCollection<CreditCardListItemViewModel>();
            SelectedCardPurchases = new ObservableCollection<CreditCardPurchase>();

            LoadCommand = new RelayCommand(async _ => await LoadCardsAsync());
            AddCommand = new RelayCommand(_ => OpenAddCard());
            EditCommand = new RelayCommand(_ => OpenEditCard(), _ => SelectedCardItem != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteCardAsync(), _ => SelectedCardItem != null);

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized || _needsRefresh)
            {
                _needsRefresh = false;
                _isInitialized = true;
                RunAsync(LoadCardsAsync);
            }
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            _needsRefresh = true;
        }

        #region Свойства

        public ObservableCollection<CreditCardListItemViewModel> CreditCardItems
        {
            get => _creditCardItems;
            set => SetProperty(ref _creditCardItems, value);
        }

        public CreditCardListItemViewModel SelectedCardItem
        {
            get => _selectedCardItem;
            set
            {
                if (SetProperty(ref _selectedCardItem, value))
                {
                    RunAsync(LoadSelectedCardPurchasesAsync);
                    (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<CreditCardPurchase> SelectedCardPurchases
        {
            get => _selectedCardPurchases;
            set => SetProperty(ref _selectedCardPurchases, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        public CreditCardEditViewModel EditViewModel
        {
            get => _editViewModel;
            set => SetProperty(ref _editViewModel, value);
        }

        public decimal TotalLimit
        {
            get => _totalLimit;
            set { if (SetProperty(ref _totalLimit, value)) OnPropertyChanged(nameof(TotalLimitFormatted)); }
        }

        public string TotalLimitFormatted => $"{_totalLimit:N0} ₽";

        public decimal TotalDebt
        {
            get => _totalDebt;
            set { if (SetProperty(ref _totalDebt, value)) OnPropertyChanged(nameof(TotalDebtFormatted)); }
        }

        public string TotalDebtFormatted => $"{_totalDebt:N0} ₽";

        public decimal TotalAvailable
        {
            get => _totalAvailable;
            set { if (SetProperty(ref _totalAvailable, value)) OnPropertyChanged(nameof(TotalAvailableFormatted)); }
        }

        public string TotalAvailableFormatted => $"{_totalAvailable:N0} ₽";

        public DateTime? NearestPaymentDate
        {
            get => _nearestPaymentDate;
            set { if (SetProperty(ref _nearestPaymentDate, value)) OnPropertyChanged(nameof(NearestPaymentDateFormatted)); }
        }

        public string NearestPaymentDateFormatted =>
            _nearestPaymentDate.HasValue ? _nearestPaymentDate.Value.ToString("d MMMM", RuCulture) : "—";

        public decimal NearestPaymentAmount
        {
            get => _nearestPaymentAmount;
            set { if (SetProperty(ref _nearestPaymentAmount, value)) OnPropertyChanged(nameof(NearestPaymentAmountFormatted)); }
        }

        public string NearestPaymentAmountFormatted => $"{_nearestPaymentAmount:N0} ₽";

        public bool HasCards => CreditCardItems.Count > 0;

        #endregion

        #region Команды

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        #endregion

        #region Методы

        private async Task LoadCardsAsync()
        {
            try
            {
                IsLoading = true;
                var cards = await _creditCardService.GetAllAsync();

                var items = cards.Select(c => new CreditCardListItemViewModel(c)).ToList();

                // Параллельно загружаем метрики для каждой карты
                await Task.WhenAll(items.Select(async item =>
                {
                    item.CurrentDebt = await _creditCardService.GetCurrentDebtAsync(item.Card.Id);
                    item.AvailableLimit = await _creditCardService.GetAvailableLimitAsync(item.Card.Id);
                    item.MinimumPayment = await _creditCardService.GetMinimumPaymentAsync(item.Card.Id);
                    item.NextPaymentDate = await _creditCardService.GetNextPaymentDateAsync(item.Card.Id);
                }));

                CreditCardItems = new ObservableCollection<CreditCardListItemViewModel>(items);
                OnPropertyChanged(nameof(HasCards));

                CalculateTotals();

                if (_selectedCardItem != null)
                {
                    var updated = CreditCardItems.FirstOrDefault(i => i.Card.Id == _selectedCardItem.Card.Id);
                    SelectedCardItem = updated;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки карт: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateTotals()
        {
            decimal totalLimit = 0;
            decimal totalDebt = 0;
            decimal totalAvailable = 0;
            DateTime? nearestDate = null;
            decimal nearestAmount = 0;

            foreach (var item in CreditCardItems)
            {
                totalLimit += item.Card.Limit;
                totalDebt += item.CurrentDebt;
                totalAvailable += item.AvailableLimit;

                if (item.NextPaymentDate.HasValue && item.CurrentDebt > 0)
                {
                    if (!nearestDate.HasValue || item.NextPaymentDate.Value < nearestDate.Value)
                    {
                        nearestDate = item.NextPaymentDate.Value;
                        nearestAmount = item.MinimumPayment;
                    }
                }
            }

            TotalLimit = totalLimit;
            TotalDebt = totalDebt;
            TotalAvailable = totalAvailable;
            NearestPaymentDate = nearestDate;
            NearestPaymentAmount = nearestAmount;
        }

        private async Task LoadSelectedCardPurchasesAsync()
        {
            SelectedCardPurchases.Clear();

            if (_selectedCardItem == null) return;

            try
            {
                var card = await _creditCardService.GetCardWithPurchasesAsync(_selectedCardItem.Card.Id);
                if (card?.Purchases == null) return;

                foreach (var purchase in card.Purchases.OrderByDescending(p => p.PurchaseDate))
                    SelectedCardPurchases.Add(purchase);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки покупок: {ex.Message}");
            }
        }

        private void OpenAddCard()
        {
            var vm = new CreditCardEditViewModel(_creditCardService, _dialogService, _toastService);
            vm.InitializeForAdd();
            vm.OnSaved = async () =>
            {
                IsModalOpen = false;
                EditViewModel = null;
                await LoadCardsAsync();
                _dataChangeService?.NotifyDataChanged();
            };
            vm.OnCancelled = () =>
            {
                IsModalOpen = false;
                EditViewModel = null;
            };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private void OpenEditCard()
        {
            if (_selectedCardItem == null) return;

            var vm = new CreditCardEditViewModel(_creditCardService, _dialogService, _toastService);
            vm.InitializeForEdit(_selectedCardItem.Card);
            vm.OnSaved = async () =>
            {
                IsModalOpen = false;
                EditViewModel = null;
                await LoadCardsAsync();
                _dataChangeService?.NotifyDataChanged();
            };
            vm.OnCancelled = () =>
            {
                IsModalOpen = false;
                EditViewModel = null;
            };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private async Task DeleteCardAsync()
        {
            if (_selectedCardItem == null) return;

            var card = _selectedCardItem.Card;
            if (!_dialogService.ShowConfirmation($"Деактивировать карту «{card.Name}»?", "Подтверждение"))
                return;

            try
            {
                await _creditCardService.DeleteAsync(card.Id);
                await LoadCardsAsync();
                _dataChangeService?.NotifyDataChanged();
                _toastService.ShowInfo($"Карта «{card.Name}» деактивирована");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        #endregion

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;
        }
    }
}
