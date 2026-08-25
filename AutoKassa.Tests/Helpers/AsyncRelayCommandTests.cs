using AutoKassa.Helpers;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Helpers
{
    public class AsyncRelayCommandTests
    {
        [Fact]
        public async Task CanExecute_BlockedWhileExecuting()
        {
            var gate = new TaskCompletionSource();
            var command = new AsyncRelayCommand(() => gate.Task);

            command.CanExecute(null).Should().BeTrue();

            command.Execute(null);
            command.CanExecute(null).Should().BeFalse("команда выполняется — повторный запуск заблокирован");

            gate.SetResult();
            await TestWaitHelper.WaitUntilAsync(() => command.CanExecute(null));
        }

        [Fact]
        public async Task Execute_WhenCanExecuteFalse_DoesNotRun()
        {
            var ran = false;
            var command = new AsyncRelayCommand(() => { ran = true; return Task.CompletedTask; }, () => false);

            command.Execute(null);
            await Task.Delay(50);

            ran.Should().BeFalse();
        }

        [Fact]
        public async Task Execute_Exception_SwallowedAndStateRestored()
        {
            var command = new AsyncRelayCommand(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            });

            // Исключение не должно вырваться наружу — команда логирует его сама
            command.Execute(null);

            await TestWaitHelper.WaitUntilAsync(() => command.CanExecute(null));
        }
    }
}
