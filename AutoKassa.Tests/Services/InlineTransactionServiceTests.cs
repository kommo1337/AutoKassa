using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Transactions;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class InlineTransactionServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly InlineTransactionService _svc;

        public InlineTransactionServiceTests()
        {
            var (factory, conn) = TestDatabase.CreateWithFactory();
            _conn = conn;
            _ctx = factory.CreateDbContext();
            _svc = new InlineTransactionService(new TransactionService(factory));
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        [Fact]
        public async Task TrySaveAsync_ValidInput_SavesTransaction()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var input = new InlineTransactionInput("100", OperationType.Expense, cat, PaymentType.Cash, null, "Тест", DateTime.Today);
            var errors = new List<string>();

            var result = await _svc.TrySaveAsync(input, errors.Add);

            result.Should().NotBeNull();
            errors.Should().BeEmpty();
        }

        [Fact]
        public async Task TrySaveAsync_TooLongDescription_ReturnsNullWithError()
        {
            var cat = TestDatabase.SeedExpenseCategory(_ctx);
            var input = new InlineTransactionInput("100", OperationType.Expense, cat, PaymentType.Cash, null, new string('X', 501), DateTime.Today);
            var errors = new List<string>();

            var result = await _svc.TrySaveAsync(input, errors.Add);

            result.Should().BeNull();
            errors.Should().ContainSingle(e => e.Contains("500"));
            _ctx.ChangeTracker.Clear();
            _ctx.Transactions.Should().BeEmpty();
        }
    }
}
