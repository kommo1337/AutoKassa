using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel экрана справочника контрагентов
    /// </summary>
    public class CounterpartiesViewModel : ViewModelBase, INavigationAware, IPageViewModel
    {
        private readonly ICounterpartyService _counterpartyService;
        private readonly IDebtService _debtService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly IDataChangeService _dataChangeService;
        private bool _isInitialized;
        private bool _needsRefresh;
        private CancellationTokenSource _searchDebounceCts;
        private int? _pendingCounterpartyId;

        private ObservableCollection<CounterpartyViewModel> _items;
        private CounterpartyViewModel _selectedItem;
        private CounterpartyType? _selectedTypeFilter;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private bool _isModalOpen;
        private CounterpartyEditViewModel _editViewModel;

        public CounterpartiesViewModel(
            ICounterpartyService counterpartyService,
            IDebtService debtService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            IDataChangeService dataChangeService)
        {
            _counterpartyService = counterpartyService;
            _debtService = debtService;
            _dialogService = dialogService;
            _toastService = toastService;
            _dataChangeService = dataChangeService;

            _items = new ObservableCollection<CounterpartyViewModel>();

            AddCommand = new RelayCommand(_ => OpenAdd());
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            FilterByTypeCommand = new RelayCommand<string>(type => ApplyTypeFilter(type));
            DeleteCommand = new AsyncRelayCommand<CounterpartyViewModel>(vm => DeleteAsync(vm), _ => true);
            EditCommand = new RelayCommand<CounterpartyViewModel>(vm => OpenEdit(vm), _ => true);

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized || _needsRefresh)
            {
                _needsRefresh = false;
                _isInitialized = true;
                RunAsync(LoadAsync);
            }
            else if (_pendingCounterpartyId.HasValue)
            {
                SelectCounterpartyById(_pendingCounterpartyId.Value);
                _pendingCounterpartyId = null;
            }
        }

        /// <summary>
        /// Принимает параметр навигации — идентификатор контрагента для выделения.
        /// </summary>
        public void Initialize(object parameter)
        {
            _pendingCounterpartyId = parameter switch
            {
                int id => id,
                string str when int.TryParse(str, out var id) => id,
                _ => null
            };
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            _needsRefresh = true;
        }

        #region Properties

        public ObservableCollection<CounterpartyViewModel> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public CounterpartyViewModel SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public CounterpartyType? SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    _ = LoadAsync();
                    OnPropertyChanged(nameof(IsAllTypeFilter));
                    OnPropertyChanged(nameof(IsClientFilter));
                    OnPropertyChanged(nameof(IsBranchFilter));
                    OnPropertyChanged(nameof(IsSupplierFilter));
                    OnPropertyChanged(nameof(IsOtherFilter));
                }
            }
        }

        /// <summary>
        /// Активен ли фильтр «Все типы».
        /// </summary>
        public bool IsAllTypeFilter => _selectedTypeFilter == null;

        /// <summary>
        /// Активен ли фильтр «Клиент».
        /// </summary>
        public bool IsClientFilter => _selectedTypeFilter == CounterpartyType.Client;

        /// <summary>
        /// Активен ли фильтр «Филиал».
        /// </summary>
        public bool IsBranchFilter => _selectedTypeFilter == CounterpartyType.Branch;

        /// <summary>
        /// Активен ли фильтр «Поставщик».
        /// </summary>
        public bool IsSupplierFilter => _selectedTypeFilter == CounterpartyType.Supplier;

        /// <summary>
        /// Активен ли фильтр «Прочее».
        /// </summary>
        public bool IsOtherFilter => _selectedTypeFilter == CounterpartyType.Other;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    DebounceSearch();
            }
        }

        private void DebounceSearch()
        {
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _searchDebounceCts, newCts);
            oldCts?.Cancel();
            oldCts?.Dispose();
            var ct = newCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(350, ct);
                    if (ct.IsCancellationRequested) return;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!ct.IsCancellationRequested) await LoadAsync();
                    });
                }
                catch (TaskCanceledException) { }
            });
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

        public CounterpartyEditViewModel EditViewModel
        {
            get => _editViewModel;
            set => SetProperty(ref _editViewModel, value);
        }

        public bool HasItems => _items != null && _items.Count > 0;

        #endregion

        #region Commands

        public ICommand AddCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand FilterByTypeCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand EditCommand { get; }

        #endregion

        #region Methods

        private async Task LoadAsync()
        {
            try
            {
                IsLoading = true;

                var counterparties = await _counterpartyService.GetAllAsync();
                var debts = await _debtService.GetDebtsAsync(status: DebtStatus.Active);

                var debtByCounterparty = debts
                    .GroupBy(d => d.CounterpartyId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(d => d.Direction == OperationType.Income ? d.RemainingAmount : -d.RemainingAmount));

                var filtered = counterparties
                    .Where(c => !_selectedTypeFilter.HasValue || c.Type == _selectedTypeFilter.Value)
                    .Where(c => string.IsNullOrWhiteSpace(_searchText) ||
                                c.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                                (c.Phone != null && c.Phone.Contains(_searchText, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(c => c.Type)
                    .ThenBy(c => c.Name)
                    .ToList();

                // Одно присваивание вместо Clear+Add: одно уведомление коллекции, меньше перестроек списка
                Items = new ObservableCollection<CounterpartyViewModel>(filtered
                    .Select(c =>
                    {
                        debtByCounterparty.TryGetValue(c.Id, out var debtAmount);
                        return new CounterpartyViewModel(c, EditCommand, DeleteCommand)
                        {
                            ActiveDebtAmount = debtAmount
                        };
                    }));

                OnPropertyChanged(nameof(HasItems));

                if (_pendingCounterpartyId.HasValue)
                {
                    SelectCounterpartyById(_pendingCounterpartyId.Value);
                    _pendingCounterpartyId = null;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки контрагентов");
                _dialogService.ShowError("Не удалось загрузить контрагентов. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void SelectCounterpartyById(int id)
        {
            // Сбрасываем фильтры, чтобы искомый контрагент точно попал в список
            _selectedTypeFilter = null;
            OnPropertyChanged(nameof(SelectedTypeFilter));
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            var item = _items.FirstOrDefault(c => c.Id == id);
            if (item != null)
                SelectedItem = item;
        }

        private void ApplyTypeFilter(string? type)
        {
            SelectedTypeFilter = type switch
            {
                "Client" => CounterpartyType.Client,
                "Branch" => CounterpartyType.Branch,
                "Supplier" => CounterpartyType.Supplier,
                "Other" => CounterpartyType.Other,
                _ => null
            };
        }

        private void OpenAdd()
        {
            var vm = new CounterpartyEditViewModel(_counterpartyService, _dialogService, null);
            vm.OnClosed = () =>
            {
                CloseEditModal();
                RunAsync(LoadAsync);
                _dataChangeService?.NotifyDataChanged();
            };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private void OpenEdit(CounterpartyViewModel? item)
        {
            if (item == null) return;

            var vm = new CounterpartyEditViewModel(_counterpartyService, _dialogService, item.Counterparty);
            vm.OnClosed = () =>
            {
                CloseEditModal();
                RunAsync(LoadAsync);
                _dataChangeService?.NotifyDataChanged();
            };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        /// <summary>
        /// Закрыть модальное окно контрагента: ViewModel освобождается и обнуляется,
        /// чтобы её представление пересоздавалось при следующем открытии.
        /// </summary>
        private void CloseEditModal()
        {
            IsModalOpen = false;
            (_editViewModel as IDisposable)?.Dispose();
            EditViewModel = null;
        }

        private async Task DeleteAsync(CounterpartyViewModel? item)
        {
            if (item == null) return;

            try
            {
                await _counterpartyService.DeleteAsync(item.Id);
                await LoadAsync();
                _dataChangeService?.NotifyDataChanged();
                _toastService.ShowInfo($"Контрагент «{item.Name}» удалён");
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-правило из сервиса — текст уже понятен пользователю
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка удаления контрагента {CounterpartyId}", item.Id);
                _dialogService.ShowError("Не удалось удалить контрагента. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
        }

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;
            var searchCts = Interlocked.Exchange(ref _searchDebounceCts, null);
            searchCts?.Cancel();
            searchCts?.Dispose();
            (_editViewModel as IDisposable)?.Dispose();
            base.OnDispose();
        }

        #endregion
    }
}
