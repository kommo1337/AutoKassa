using AutoKassa.Services;

namespace AutoKassa.Tests.Services.Backup.YandexDisk
{
    /// <summary>Фейк IClipboardService: запоминает последний скопированный текст.</summary>
    public sealed class FakeClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }
        public int SetTextCallCount { get; private set; }

        /// <summary>Если false — SetText возвращает неудачу (эмуляция занятого буфера).</summary>
        public bool Succeeds { get; set; } = true;

        public bool SetText(string text)
        {
            SetTextCallCount++;
            if (!Succeeds)
                return false;

            LastText = text;
            return true;
        }
    }
}
