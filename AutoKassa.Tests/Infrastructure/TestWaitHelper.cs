namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Ожидание условия в тестах для async void команд (AsyncRelayCommand.Execute
    /// не возвращает Task, поэтому выполнение отслеживается по побочным эффектам).
    /// </summary>
    public static class TestWaitHelper
    {
        public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!condition())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException("Условие не выполнено за отведённое время");
                await Task.Delay(25);
            }
        }
    }
}
