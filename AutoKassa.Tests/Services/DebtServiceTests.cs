using System;
using System.Linq;
using System.Threading.Tasks;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class DebtServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly DebtService _svc;

        public DebtServiceTests()
        {
            var (factory, conn) = TestDatabase.CreateWithFactory();
            _conn = conn;
            _ctx = factory.CreateDbContext();
            _svc = new DebtService(factory);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        private int _debtIndex;

        private Transaction CreateDebt(OperationType type, decimal amount, string counterpartyName = "Контрагент")
        {
            var counterparty = TestDatabase.SeedCounterparty(_ctx, counterpartyName);
            var categoryName = $"Кат-{type}-{_debtIndex++}";
            var category = type == OperationType.Income
                ? TestDatabase.SeedIncomeCategory(_ctx, categoryName)
                : TestDatabase.SeedExpenseCategory(_ctx, categoryName);

            return TestDatabase.SeedTransaction(
                _ctx, category.Id, amount, type, PaymentType.Debt,
                counterpartyId: counterparty.Id, debtStatus: DebtStatus.Active);
        }

        [Fact]
        public async Task GetDebtsAsync_ReturnsOnlyDebtTransactions()
        {
            var debt = CreateDebt(OperationType.Income, 1000m);
            var category = TestDatabase.SeedIncomeCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, category.Id, 500m, OperationType.Income, PaymentType.Cash);

            var debts = await _svc.GetDebtsAsync();

            debts.Should().ContainSingle(d => d.TransactionId == debt.Id);
        }

        [Fact]
        public async Task GetDebtsAsync_FiltersByDirection()
        {
            var incomeDebt = CreateDebt(OperationType.Income, 1000m, "Клиент");
            CreateDebt(OperationType.Expense, 500m, "Поставщик");

            var debts = await _svc.GetDebtsAsync(OperationType.Income);

            debts.Should().ContainSingle(d => d.TransactionId == incomeDebt.Id);
        }

        [Fact]
        public async Task GetRemainingAmountAsync_NewDebt_ReturnsFullAmount()
        {
            var debt = CreateDebt(OperationType.Income, 1000m);

            var remaining = await _svc.GetRemainingAmountAsync(debt.Id);

            remaining.Should().Be(1000m);
        }

        [Fact]
        public async Task RepayAsync_PartialPayment_CreatesRepaymentAndKeepsActive()
        {
            var debt = CreateDebt(OperationType.Income, 1000m);

            var repayment = await _svc.RepayAsync(debt.Id, 400m, PaymentType.Cash, DateTime.Today);

            repayment.Type.Should().Be(OperationType.Income);
            repayment.PaymentType.Should().Be(PaymentType.Cash);
            repayment.DebtStatus.Should().Be(DebtStatus.NotDebt);

            var remaining = await _svc.GetRemainingAmountAsync(debt.Id);
            remaining.Should().Be(600m);

            var debts = await _svc.GetDebtsAsync(status: DebtStatus.Active);
            debts.Should().ContainSingle(d => d.TransactionId == debt.Id);
        }

        [Fact]
        public async Task RepayAsync_FullPayment_MarksDebtRepaid()
        {
            var debt = CreateDebt(OperationType.Expense, 500m);

            await _svc.RepayAsync(debt.Id, 500m, PaymentType.NonCash, DateTime.Today);

            var debts = await _svc.GetDebtsAsync(status: DebtStatus.Repaid);
            debts.Should().ContainSingle(d => d.TransactionId == debt.Id);
        }

        [Fact]
        public async Task RepayAsync_OverPayment_ThrowsInvalidOperationException()
        {
            var debt = CreateDebt(OperationType.Income, 300m);

            Func<Task> act = async () => await _svc.RepayAsync(debt.Id, 400m, PaymentType.Cash, DateTime.Today);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task RepayAsync_DebtPaymentType_ThrowsArgumentException()
        {
            var debt = CreateDebt(OperationType.Income, 300m);

            Func<Task> act = async () => await _svc.RepayAsync(debt.Id, 100m, PaymentType.Debt, DateTime.Today);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task WriteOffAsync_SetsStatusWrittenOff()
        {
            var debt = CreateDebt(OperationType.Income, 1000m);

            await _svc.WriteOffAsync(debt.Id);

            var remaining = await _svc.GetRemainingAmountAsync(debt.Id);
            remaining.Should().Be(0m);

            var debts = await _svc.GetDebtsAsync(status: DebtStatus.WrittenOff);
            debts.Should().ContainSingle(d => d.TransactionId == debt.Id);
        }

        [Fact]
        public async Task RecalculateStatusAsync_AfterDeletingRepayment_ReturnsActive()
        {
            var debt = CreateDebt(OperationType.Income, 1000m);
            var repayment = await _svc.RepayAsync(debt.Id, 1000m, PaymentType.Cash, DateTime.Today);

            var repaymentInContext = _ctx.Transactions.Find(repayment.Id);
            repaymentInContext!.IsDeleted = true;
            _ctx.SaveChanges();

            await _svc.RecalculateStatusAsync(debt.Id);

            var remaining = await _svc.GetRemainingAmountAsync(debt.Id);
            remaining.Should().Be(1000m);

            var debts = await _svc.GetDebtsAsync(status: DebtStatus.Active);
            debts.Should().ContainSingle(d => d.TransactionId == debt.Id);
        }

        [Fact]
        public async Task GetDebtsAsync_CalculatesRepaidAmount()
        {
            var debt = CreateDebt(OperationType.Expense, 800m);
            await _svc.RepayAsync(debt.Id, 300m, PaymentType.Cash, DateTime.Today);

            var debts = await _svc.GetDebtsAsync();
            var item = debts.First(d => d.TransactionId == debt.Id);

            item.RepaidAmount.Should().Be(300m);
            item.Amount.Should().Be(800m);
        }

        [Fact]
        public async Task RepayAsync_RepaidDebt_ThrowsInvalidOperationException()
        {
            var debt = CreateDebt(OperationType.Income, 500m);
            await _svc.RepayAsync(debt.Id, 500m, PaymentType.Cash, DateTime.Today);

            Func<Task> act = async () => await _svc.RepayAsync(debt.Id, 100m, PaymentType.Cash, DateTime.Today);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task RepayAsync_WrittenOffDebt_ThrowsInvalidOperationException()
        {
            var debt = CreateDebt(OperationType.Income, 500m);
            await _svc.WriteOffAsync(debt.Id);

            Func<Task> act = async () => await _svc.RepayAsync(debt.Id, 100m, PaymentType.Cash, DateTime.Today);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task WriteOffAsync_WithRepayments_ThrowsInvalidOperationException()
        {
            var debt = CreateDebt(OperationType.Expense, 1000m);
            await _svc.RepayAsync(debt.Id, 200m, PaymentType.Cash, DateTime.Today);

            Func<Task> act = async () => await _svc.WriteOffAsync(debt.Id);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task GetDebtsAsync_Pagination_ReturnsRequestedPage()
        {
            for (int i = 0; i < 5; i++)
                CreateDebt(OperationType.Income, 100m + i, $"Контрагент-{i}");

            var page1 = await _svc.GetDebtsAsync(pageNumber: 1, pageSize: 2);
            var page2 = await _svc.GetDebtsAsync(pageNumber: 2, pageSize: 2);
            var page3 = await _svc.GetDebtsAsync(pageNumber: 3, pageSize: 2);

            page1.Should().HaveCount(2);
            page2.Should().HaveCount(2);
            page3.Should().HaveCount(1);
        }
    }
}
