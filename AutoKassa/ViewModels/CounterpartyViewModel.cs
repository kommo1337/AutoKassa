using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel строки контрагента в списке
    /// </summary>
    public class CounterpartyViewModel : ViewModelBase
    {
        private Counterparty _counterparty;
        private decimal _activeDebtAmount;

        public CounterpartyViewModel(
            Counterparty counterparty,
            ICommand editCommand,
            ICommand deleteCommand)
        {
            _counterparty = counterparty;
            EditCommand = editCommand;
            DeleteCommand = deleteCommand;
        }

        /// <summary>
        /// Модель контрагента
        /// </summary>
        public Counterparty Counterparty
        {
            get => _counterparty;
            set
            {
                if (SetProperty(ref _counterparty, value))
                {
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(Phone));
                    OnPropertyChanged(nameof(Notes));
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public int Id => _counterparty.Id;
        public string Name => _counterparty.Name;
        public CounterpartyType Type => _counterparty.Type;
        public string? Phone => _counterparty.Phone;
        public string? Notes => _counterparty.Notes;
        public bool IsActive => _counterparty.IsActive;

        /// <summary>
        /// Текущий активный долг контрагента (положительный — нам должны, отрицательный — мы должны)
        /// </summary>
        public decimal ActiveDebtAmount
        {
            get => _activeDebtAmount;
            set => SetProperty(ref _activeDebtAmount, value);
        }

        /// <summary>
        /// Есть ли активный долг у контрагента
        /// </summary>
        public bool HasActiveDebt => _activeDebtAmount != 0;

        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
    }
}
