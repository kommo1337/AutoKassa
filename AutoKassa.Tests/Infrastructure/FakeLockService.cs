using AutoKassa.Services;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Фейк ILockService для тестов: считает вызовы методов,
    /// реальной блокировки и таймеров не создаёт.
    /// </summary>
    public class FakeLockService : ILockService
    {
        public int LockCount { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int ResetCount { get; private set; }

        public bool IsLocked { get; set; }

        public void Lock() => LockCount++;

        public void StartAutoLockTimer() => StartCount++;

        public void StopAutoLockTimer() => StopCount++;

        public void ResetAutoLockTimer() => ResetCount++;

        public void Dispose() { }
    }
}
