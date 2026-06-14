using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models.Reports;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для окна «Сверка кассы»
    /// </summary>
    public class ReconciliationViewModel : ViewModelBase
    {
        private readonly IReportService _reportService;
        private readonly CancellationTokenSource _cts;

        private DateTime _reconciliationDate;
        private decimal _cashAmount;
        private decimal _nonCashAmount;
        private decimal _creditDebt;
        private decimal _nextPaymentAmount;
        private DateTime? _nextPaymentDate;
        private decimal _factBalance;
        private decimal _netBalance;
        private string _netBalanceFormula = string.Empty;
        private bool _isLoading;

        public ReconciliationViewModel(IReportService reportService)
        {
            _reportService = reportService;
            _cts = new CancellationTokenSource();

            _reconciliationDate = DateTime.Today;

            SetTodayCommand = new RelayCommand(_ => ReconciliationDate = DateTime.Today);

            // Загружаем данные при первом отображении
            LoadDataAsync();
        }

        #region Свойства

        /// <summary>
        /// Дата сверки
        /// </summary>
        public DateTime ReconciliationDate
        {
            get => _reconciliationDate;
            set
            {
                if (SetProperty(ref _reconciliationDate, value))
                {
                    LoadDataAsync();
                }
            }
        }

        /// <summary>
        /// Остаток наличных на конец даты
        /// </summary>
        public decimal CashAmount
        {
            get => _cashAmount;
            set => SetProperty(ref _cashAmount, value);
        }

        /// <summary>
        /// Остаток безналичных на конец даты
        /// </summary>
        public decimal NonCashAmount
        {
            get => _nonCashAmount;
            set => SetProperty(ref _nonCashAmount, value);
        }

        /// <summary>
        /// Общий текущий долг по кредитным картам
        /// </summary>
        public decimal CreditDebt
        {
            get => _creditDebt;
            set => SetProperty(ref _creditDebt, value);
        }

        /// <summary>
        /// Сумма минимального платежа на ближайшую дату
        /// </summary>
        public decimal NextPaymentAmount
        {
            get => _nextPaymentAmount;
            set => SetProperty(ref _nextPaymentAmount, value);
        }

        /// <summary>
        /// Дата ближайшего платежа по кредитным картам
        /// </summary>
        public DateTime? NextPaymentDate
        {
            get => _nextPaymentDate;
            set => SetProperty(ref _nextPaymentDate, value);
        }

        /// <summary>
        /// Фактический остаток (наличные + безналичные)
        /// </summary>
        public decimal FactBalance
        {
            get => _factBalance;
            set => SetProperty(ref _factBalance, value);
        }

        /// <summary>
        /// Чистый баланс (фактический остаток минус кредитный долг)
        /// </summary>
        public decimal NetBalance
        {
            get => _netBalance;
            set => SetProperty(ref _netBalance, value);
        }

        /// <summary>
        /// Формула расчёта чистого баланса
        /// </summary>
        public string NetBalanceFormula
        {
            get => _netBalanceFormula;
            set => SetProperty(ref _netBalanceFormula, value);
        }

        /// <summary>
        /// Идёт ли загрузка данных
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Есть ли данные для отображения
        /// </summary>
        public bool HasData => !IsLoading;

        #endregion

        #region Команды

        /// <summary>
        /// Установить дату сверки на сегодня
        /// </summary>
        public ICommand SetTodayCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Асинхронная загрузка данных сверки
        /// </summary>
        private void LoadDataAsync()
        {
            RunAsync(async () =>
            {
                IsLoading = true;
                try
                {
                    var data = await _reportService.GetReconciliationDataAsync(ReconciliationDate, _cts.Token).ConfigureAwait(false);
                    ApplyData(data);
                }
                finally
                {
                    IsLoading = false;
                }
            });
        }

        /// <summary>
        /// Применить полученные данные к свойствам ViewModel
        /// </summary>
        private void ApplyData(ReconciliationData data)
        {
            CashAmount = data.CashAmount;
            NonCashAmount = data.NonCashAmount;
            CreditDebt = data.CreditDebt;
            NextPaymentAmount = data.NextPaymentAmount;
            NextPaymentDate = data.NextPaymentDate;

            FactBalance = CashAmount + NonCashAmount;
            NetBalance = FactBalance - CreditDebt;
            NetBalanceFormula = $"{FactBalance:N2} ₽ − {CreditDebt:N2} ₽";
        }

        protected override void OnDispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            base.OnDispose();
        }

        #endregion
    }
}
