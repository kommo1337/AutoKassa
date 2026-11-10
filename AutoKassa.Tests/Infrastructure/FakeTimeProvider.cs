namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Ручной TimeProvider для тестов: виртуальные часы без автопродвижения.
    /// Таймеры (Task.Delay) при этом живут на реальном времени — тесты ждут реальные задержки.
    /// </summary>
    public class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset startUtc)
        {
            _utcNow = startUtc;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

        public static FakeTimeProvider FromNow() => new(DateTimeOffset.UtcNow);
    }
}
