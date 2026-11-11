using AutoKassa.Helpers;
using AutoKassa.Services.Backup.YandexDisk;
using System.Threading;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Диалог ввода ClientId приложения Яндекс Диска.
    /// Открывается из настроек по «Подключить», когда ClientId не задан ни в appsettings.json,
    /// ни ранее пользователем. Сохранение выполняет <see cref="IYandexDiskClientIdStore"/> —
    /// подключение продолжается без перезапуска приложения.
    /// </summary>
    public class YandexClientIdDialogViewModel : ViewModelBase
    {
        private readonly IYandexDiskClientIdStore _clientIdStore;
        // Сохранение завершается на пуле потоков (ConfigureAwait(false) в хранилище) —
        // закрытие диалога обязано вернуться на UI-поток: подписчик трогает привязанные свойства.
        private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
        private string _clientId = string.Empty;
        private string? _clientIdError;

        public YandexClientIdDialogViewModel(IYandexDiskClientIdStore clientIdStore)
        {
            _clientIdStore = clientIdStore;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(_ => OnClose?.Invoke(false));
        }

        /// <summary>Обратный вызов закрытия диалога (на UI-потоке): true — сохранено, false — отмена.</summary>
        public Action<bool>? OnClose { get; set; }

        /// <summary>Поднять закрытие диалога, вернувшись на UI-поток, если он захвачен.</summary>
        private void RaiseClose(bool result)
        {
            if (_uiContext != null)
                _uiContext.Post(_ => OnClose?.Invoke(result), null);
            else
                OnClose?.Invoke(result);
        }

        public string ClientId
        {
            get => _clientId;
            set
            {
                if (SetProperty(ref _clientId, value))
                    ValidateClientId();
            }
        }

        public string? ClientIdError
        {
            get => _clientIdError;
            private set => SetProperty(ref _clientIdError, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private void ValidateClientId()
        {
            ClientIdError = string.IsNullOrWhiteSpace(_clientId)
                ? "Введите ClientId приложения"
                : null;
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(ClientId) && ClientIdError == null;

        private async Task SaveAsync()
        {
            try
            {
                ValidateClientId();
                if (ClientIdError != null)
                    return;

                await _clientIdStore.SaveAsync(ClientId.Trim()).ConfigureAwait(false);
                RaiseClose(true);
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-правило из хранилища — текст уже понятен пользователю
                ClientIdError = ex.Message;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка сохранения ClientId Яндекс Диска");
                ClientIdError = "Не удалось сохранить ClientId. Попробуйте ещё раз.";
            }
        }
    }
}
