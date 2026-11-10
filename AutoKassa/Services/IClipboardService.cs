namespace AutoKassa.Services
{
    /// <summary>
    /// Доступ к системному буферу обмена. Взаимодействие с Clipboard — UI-зависимое
    /// (STA-поток, возможен отказ при занятом буфере), поэтому вынесено за интерфейс:
    /// ViewModel остаётся тестируемой, а реализация выполняется на UI-потоке.
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>Поместить текст в буфер обмена. false — не удалось (буфер занят, нет STA-потока).</summary>
        bool SetText(string text);
    }
}
