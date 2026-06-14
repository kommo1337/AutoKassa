using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class CreditCardServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly TestDbContextFactory _factory;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly CreditCardService _svc;

        public CreditCardServiceTests()
        {
            var (factory, conn) = TestDatabase.CreateWithFactory();
            _conn = conn;
            _factory = factory;
            _ctx = factory.CreateDbContext();
            _svc = new CreditCardService(factory);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        private CreditCard SeedCard(decimal limit = 100000m, decimal initialDebt = 0m, decimal minPercent = 5m)
        {
            var card = new CreditCard
            {
                Name = "Тестовая карта",
                BankName = "ТестБанк",
                Limit = limit,
                InitialDebt = initialDebt,
                MinimumPaymentPercent = minPercent,
                PaymentDay = 10,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _ctx.CreditCards.Add(card);
            _ctx.SaveChanges();
            return card;
        }

        private Transaction SeedPurchaseTransaction(int categoryId, decimal amount, PaymentType paymentType = PaymentType.CreditCard)
        {
            var t = new Transaction
            {
                CategoryId = categoryId,
                Amount = amount,
                Type = OperationType.Expense,
                PaymentType = paymentType,
                Date = DateTime.Today,
                Description = "Покупка",
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };
            _ctx.Transactions.Add(t);
            _ctx.SaveChanges();
            return t;
        }

        private Transaction SeedRepaymentTransaction(int creditCardId, decimal amount)
        {
            var category = _ctx.Categories.First(c => c.Name == CreditCardService.RepaymentCategoryName);
            var t = new Transaction
            {
                CategoryId = category.Id,
                CreditCardId = creditCardId,
                Amount = amount,
                Type = OperationType.Expense,
                PaymentType = PaymentType.Cash,
                Date = DateTime.Today,
                Description = "Погашение",
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };
            _ctx.Transactions.Add(t);
            _ctx.SaveChanges();
            return t;
        }

        [Fact]
        public async Task CreateAsync_AddsCard()
        {
            var card = new CreditCard
            {
                Name = "Новая карта",
                Limit = 50000m,
                InitialDebt = 1000m,
                MinimumPaymentPercent = 3m
            };

            var result = await _svc.CreateAsync(card);

            result.Id.Should().BeGreaterThan(0);
            result.IsActive.Should().BeTrue();
            var fromDb = await _ctx.CreditCards.FindAsync(result.Id);
            fromDb.Should().NotBeNull();
            fromDb!.Name.Should().Be("Новая карта");
        }

        [Fact]
        public async Task GetCurrentDebtAsync_AfterPurchase_Increases()
        {
            var card = SeedCard(initialDebt: 5000m);
            var category = TestDatabase.SeedExpenseCategory(_ctx);
            var transaction = SeedPurchaseTransaction(category.Id, 3000m);

            await _svc.AddPurchaseAsync(card.Id, transaction.Id, transaction.Amount);

            var debt = await _svc.GetCurrentDebtAsync(card.Id);
            debt.Should().Be(8000m);
        }

        [Fact]
        public async Task GetCurrentDebtAsync_AfterRepayment_Decreases()
        {
            var card = SeedCard(initialDebt: 10000m);
            SeedRepaymentTransaction(card.Id, 2500m);

            var debt = await _svc.GetCurrentDebtAsync(card.Id);

            debt.Should().Be(7500m);
        }

        [Fact]
        public async Task GetAvailableLimitAsync_RespectsLimitAndDebt()
        {
            var card = SeedCard(limit: 10000m, initialDebt: 4000m);

            var available = await _svc.GetAvailableLimitAsync(card.Id);

            available.Should().Be(6000m);
        }

        [Fact]
        public async Task GetMinimumPaymentAsync_CalculatesCorrectly()
        {
            var card = SeedCard(initialDebt: 20000m, minPercent: 10m);

            var minimum = await _svc.GetMinimumPaymentAsync(card.Id);

            minimum.Should().Be(2000m);
        }

        [Fact]
        public async Task RepayDebtAsync_CannotExceedDebt()
        {
            var card = SeedCard(initialDebt: 1000m);

            Func<Task> act = () => _svc.RepayDebtAsync(card.Id, 2000m);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task RepayDebtAsync_UpdatesLastPaymentDate()
        {
            var card = SeedCard(initialDebt: 1000m);

            await _svc.RepayDebtAsync(card.Id, 500m);

            using var checkCtx = _factory.CreateDbContext();
            var updated = await checkCtx.CreditCards.FindAsync(card.Id);
            updated!.LastPaymentDate.Should().NotBeNull();
        }

        [Fact]
        public async Task AddPurchaseAsync_ExceedingLimit_Throws()
        {
            var card = SeedCard(limit: 5000m, initialDebt: 0m);
            var category = TestDatabase.SeedExpenseCategory(_ctx);
            var transaction = SeedPurchaseTransaction(category.Id, 6000m);

            Func<Task> act = () => _svc.AddPurchaseAsync(card.Id, transaction.Id, transaction.Amount);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task GetNextPaymentDateAsync_ReturnsFutureDate()
        {
            var card = SeedCard();
            card.PaymentDay = 15;
            _ctx.SaveChanges();

            var nextDate = await _svc.GetNextPaymentDateAsync(card.Id);

            nextDate.Should().NotBeNull();
            nextDate!.Value.Day.Should().Be(15);
            nextDate.Value.Should().BeOnOrAfter(DateTime.Today);
        }

        [Fact]
        public async Task DeleteAsync_DeactivatesCard_WhenNoDebt()
        {
            var card = SeedCard(initialDebt: 0m);

            await _svc.DeleteAsync(card.Id);

            var updated = await _ctx.CreditCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == card.Id);
            updated!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteAsync_WithDebt_Throws()
        {
            var card = SeedCard(initialDebt: 1000m);

            Func<Task> act = () => _svc.DeleteAsync(card.Id);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
