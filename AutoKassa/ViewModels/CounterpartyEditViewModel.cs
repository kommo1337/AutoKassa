using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel формы создания/редактирования контрагента
    /// </summary>
    public class CounterpartyEditViewModel : ViewModelBase
    {
        private readonly ICounterpartyService _counterpartyService;
        private readonly IDialogService _dialogService;
        private readonly Counterparty? _existingCounterparty;

        private string _name = string.Empty;
        private CounterpartyType _type = CounterpartyType.Client;
        private string? _phone;
        private string? _notes;
        private bool _isActive = true;
        private bool _isSaving;

        public CounterpartyEditViewModel(
            ICounterpartyService counterpartyService,
            IDialogService dialogService,
            Counterparty? existingCounterparty)
        {
            _counterpartyService = counterpartyService;
            _dialogService = dialogService;
            _existingCounterparty = existingCounterparty;

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => OnClosed?.Invoke());

            if (existingCounterparty != null)
            {
                _name = existingCounterparty.Name;
                _type = existingCounterparty.Type;
                _phone = existingCounterparty.Phone;
                _notes = existingCounterparty.Notes;
                _isActive = existingCounterparty.IsActive;
            }
        }

        #region Properties

        public bool IsEditMode => _existingCounterparty != null;
        public string Title => IsEditMode ? "Редактировать контрагента" : "Новый контрагент";

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                    ValidateName();
            }
        }

        public CounterpartyType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string NameError => GetFirstError(nameof(Name));

        public IEnumerable<CounterpartyType> AvailableTypes => Enum.GetValues<CounterpartyType>();

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Events

        public Action? OnClosed { get; set; }

        #endregion

        #region Methods

        private void ValidateName()
        {
            if (string.IsNullOrWhiteSpace(_name))
                SetErrors(nameof(Name), new[] { "Название контрагента обязательно" });
            else if (_name.Length > 150)
                SetErrors(nameof(Name), new[] { "Название не должно превышать 150 символов" });
            else
                ClearErrors(nameof(Name));

            OnPropertyChanged(nameof(NameError));
        }

        private bool CanSave() => !_isSaving && !HasErrors && !string.IsNullOrWhiteSpace(_name);

        private async Task SaveAsync()
        {
            if (_isSaving) return;

            ValidateName();
            if (HasErrors) return;

            _isSaving = true;
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                var counterparty = new Counterparty
                {
                    Id = _existingCounterparty?.Id ?? 0,
                    Name = _name.Trim(),
                    Type = _type,
                    Phone = string.IsNullOrWhiteSpace(_phone) ? null : _phone.Trim(),
                    Notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim(),
                    IsActive = _isActive
                };

                if (IsEditMode)
                    await _counterpartyService.UpdateAsync(counterparty).ConfigureAwait(false);
                else
                    await _counterpartyService.AddAsync(counterparty).ConfigureAwait(false);

                OnClosed?.Invoke();
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
