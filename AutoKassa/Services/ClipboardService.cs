using Serilog;
using System.Windows;

namespace AutoKassa.Services
{
    /// <summary>
    /// Реализация буфера обмена через System.Windows.Clipboard.
    /// Вызывать только с UI-потока (команды ViewModel выполняются там).
    /// </summary>
    public sealed class ClipboardService : IClipboardService
    {
        private static readonly ILogger _log = Log.ForContext<ClipboardService>();

        public bool SetText(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                // Буфер занят другим процессом (COM-ошибка CLIPBRD_E_CANT_OPEN) или нет STA-потока.
                _log.Warning(ex, "Не удалось поместить текст в буфер обмена");
                return false;
            }
        }
    }
}
