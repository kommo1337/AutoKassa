using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Transactions;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class TransactionListServiceTests
    {
        private sealed class FakeTransactionService : ITransactionService
        {
            public TransactionFilterParameters? LastFilters { get; private set; }

            public Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters, CancellationToken ct = default)
            {
                LastFilters = filters;
                return Task.FromResult(new List<Transaction>());
            }

            public Task<int> GetTotalCountAsync(TransactionFilterParameters filters, CancellationToken ct = default)
            {
                LastFilters = filters;
                return Task.FromResult(0);
            }

            public Task<Transaction> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<Transaction> AddAsync(Transaction transaction, CancellationToken ct = default) => throw new NotImplementedException();
            public Task UpdateAsync(Transaction transaction, CancellationToken ct = default) => throw new NotImplementedException();
            public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
            public Task RestoreAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<bool> IsRepaymentTransactionAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<List<Transaction>> GetRecentAsync(int count = 10, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<(decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)> GetPeriodTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<List<DailyTotalsItem>> GetDailyTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default) => throw new NotImplementedException();
            public Task<List<(string Name, decimal Total)>> GetTopCategoriesAsync(DateTime from, DateTime to, OperationType type, int count, PaymentType? paymentType = null, CancellationToken ct = default) => throw new NotImplementedException();
        }

        [Fact]
        public async Task LoadFirstPageAsync_ShouldResetSkipAndSetTake()
        {
            var fake = new FakeTransactionService();
            var svc = new TransactionListService(fake);
            var filters = new TransactionFilterParameters
            {
                Type = OperationType.Expense,
                Skip = 999,
                Take = 500
            };
            var request = new TransactionListRequest(filters, 25, 3);

            var result = await svc.LoadFirstPageAsync(request);

            result.PageSize.Should().Be(25);
            result.CurrentPage.Should().Be(0);
            fake.LastFilters.Should().NotBeNull();
            fake.LastFilters!.Skip.Should().Be(0);
            fake.LastFilters.Take.Should().Be(25);
            fake.LastFilters.Type.Should().Be(OperationType.Expense);
        }

        [Fact]
        public async Task LoadNextPageAsync_ShouldIncreasePageAndCalculateSkip()
        {
            var fake = new FakeTransactionService();
            var svc = new TransactionListService(fake);
            var filters = new TransactionFilterParameters
            {
                Skip = 0,
                Take = 25
            };
            var request = new TransactionListRequest(filters, 25, 1);

            var result = await svc.LoadNextPageAsync(request);

            result.CurrentPage.Should().Be(2);
            result.PageSize.Should().Be(25);
            fake.LastFilters.Should().NotBeNull();
            fake.LastFilters!.Skip.Should().Be(50);
            fake.LastFilters.Take.Should().Be(25);
        }

        [Fact]
        public async Task LoadFirstPageAsync_ShouldNotMutateOriginalFilters()
        {
            var fake = new FakeTransactionService();
            var svc = new TransactionListService(fake);
            var filters = new TransactionFilterParameters
            {
                Skip = 10,
                Take = 20
            };
            var request = new TransactionListRequest(filters, 15, 0);

            await svc.LoadFirstPageAsync(request);

            filters.Skip.Should().Be(10);
            filters.Take.Should().Be(20);
        }
    }
}
