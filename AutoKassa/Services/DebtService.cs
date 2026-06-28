using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с долгами (дебиторская / кредиторская задолженность)
    /// </summary>
    public class DebtService : IDebtService
    {
        private static readonly ILogger _log = Log.ForContext<DebtService>();

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public DebtService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Получает список долгов с остатком и статусом.
        /// </summary>
        public async Task<IReadOnlyList<DebtItem>> GetDebtsAsync(
            OperationType? direction = null,
            int? counterpartyId = null,
            DebtStatus? status = null,
            int pageNumber = 1,
            int pageSize = int.MaxValue,
            CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var query = context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.Counterparty)
                .Where(t => !t.IsDeleted && t.PaymentType == PaymentType.Debt && t.DebtStatus != DebtStatus.NotDebt);

            if (direction.HasValue)
                query = query.Where(t => t.Type == direction.Value);

            if (counterpartyId.HasValue)
                query = query.Where(t => t.CounterpartyId == counterpartyId.Value);

            if (status.HasValue)
                query = query.Where(t => t.DebtStatus == status.Value);

            if (pageNumber < 1)
                pageNumber = 1;
            if (pageSize < 1)
                pageSize = 1;

            query = query
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedAt);

            // Применяем пагинацию только при явном ограничении размера страницы,
            // чтобы вызовы по умолчанию (int.MaxValue) получали полный список.
            if (pageSize != int.MaxValue)
            {
                var skip = (pageNumber - 1) * pageSize;
                query = query.Skip(skip).Take(pageSize);
            }

            var debts = await query
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var debtIds = debts.Select(d => d.Id).ToList();

            // Суммы погашений по каждому долгу (только неудалённые операции-погашения)
            var repaidAmounts = await context.DebtPayments
                .AsNoTracking()
                .Where(dp => debtIds.Contains(dp.DebtTransactionId))
                .Where(dp => !dp.RepaymentTransaction.IsDeleted)
                .GroupBy(dp => dp.DebtTransactionId)
                .Select(g => new { DebtId = g.Key, Total = (decimal)g.Sum(dp => (double)dp.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var repaidDict = repaidAmounts.ToDictionary(r => r.DebtId, r => r.Total);

            return debts.Select(t => new DebtItem
            {
                TransactionId = t.Id,
                Date = t.Date,
                Amount = t.Amount,
                RepaidAmount = repaidDict.GetValueOrDefault(t.Id),
                Status = t.DebtStatus,
                Direction = t.Type,
                CounterpartyId = t.CounterpartyId,
                CounterpartyName = t.Counterparty?.Name ?? "—",
                CounterpartyType = t.Counterparty?.Type ?? CounterpartyType.Other,
                CategoryName = t.Category?.Name ?? "—",
                Description = t.Description
            }).ToList();
        }

        /// <summary>
        /// Создаёт операцию-погашение для указанного долга.
        /// </summary>
        public async Task<Transaction> RepayAsync(
            int debtTransactionId,
            decimal amount,
            PaymentType paymentType,
            DateTime date,
            string? description = null,
            CancellationToken ct = default)
        {
            if (amount <= 0)
                throw new ArgumentException("Сумма погашения должна быть больше нуля", nameof(amount));

            if (paymentType == PaymentType.Debt)
                throw new ArgumentException("Операция погашения не может иметь тип оплаты «Долг»", nameof(paymentType));

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var debt = await context.Transactions
                .Include(t => t.Category)
                .Include(t => t.Counterparty)
                .FirstOrDefaultAsync(t => t.Id == debtTransactionId && !t.IsDeleted, ct)
                .ConfigureAwait(false);

            if (debt == null)
                throw new InvalidOperationException($"Долг ID={debtTransactionId} не найден");

            if (debt.PaymentType != PaymentType.Debt)
                throw new InvalidOperationException("Операция не является долгом");

            if (debt.DebtStatus != DebtStatus.Active)
                throw new InvalidOperationException("Долг не может быть погашён: он уже погашен или списан");

            // Остаток считаем в том же контексте, без лишнего обращения к БД.
            var repaidBefore = await context.DebtPayments
                .AsNoTracking()
                .Where(dp => dp.DebtTransactionId == debtTransactionId && !dp.RepaymentTransaction.IsDeleted)
                .Select(dp => (double?)dp.Amount)
                .SumAsync(ct)
                .ConfigureAwait(false) ?? 0;

            var remaining = debt.Amount - (decimal)repaidBefore;
            if (amount > remaining)
                throw new InvalidOperationException($"Сумма погашения ({amount:N2}) превышает остаток долга ({remaining:N2})");

            // Погашение — обычная операция с тем же типом, что и долг
            var repayment = new Transaction
            {
                Date = date,
                Amount = amount,
                Type = debt.Type,
                PaymentType = paymentType,
                CategoryId = debt.CategoryId,
                Description = description,
                CounterpartyId = debt.CounterpartyId,
                DebtStatus = DebtStatus.NotDebt,
                CreatedAt = DateTime.Now,
                IsDeleted = false
            };

            context.Transactions.Add(repayment);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            var debtPayment = new DebtPayment
            {
                DebtTransactionId = debtTransactionId,
                RepaymentTransactionId = repayment.Id,
                Amount = amount
            };

            context.DebtPayments.Add(debtPayment);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // Пересчитываем статус долга в том же контексте, не создавая новый.
            var previousStatus = debt.DebtStatus;
            var repaidTotal = repaidBefore + (double)amount;
            debt.DebtStatus = repaidTotal >= (double)debt.Amount ? DebtStatus.Repaid : DebtStatus.Active;
            debt.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            if (previousStatus != debt.DebtStatus)
            {
                _log.Information(
                    "Статус долга ID={DebtId} изменён с {OldStatus} на {NewStatus}",
                    debtTransactionId, previousStatus, debt.DebtStatus);
            }

            // Загружаем навигационные свойства для возвращаемой операции
            await context.Entry(repayment).Reference(t => t.Category).LoadAsync(ct).ConfigureAwait(false);
            await context.Entry(repayment).Reference(t => t.Counterparty).LoadAsync(ct).ConfigureAwait(false);

            _log.Information(
                "Создано погашение ID={RepaymentId} для долга ID={DebtId}, сумма={Amount}",
                repayment.Id, debtTransactionId, amount);

            return repayment;
        }

        /// <summary>
        /// Списывает долг (без создания операции движения денег).
        /// </summary>
        public async Task WriteOffAsync(int debtTransactionId, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var debt = await context.Transactions
                .FirstOrDefaultAsync(t => t.Id == debtTransactionId && !t.IsDeleted, ct)
                .ConfigureAwait(false);

            if (debt == null)
                throw new InvalidOperationException($"Долг ID={debtTransactionId} не найден");

            if (debt.PaymentType != PaymentType.Debt)
                throw new InvalidOperationException("Операция не является долгом");

            // Списывать долг с существующими погашениями нельзя: иначе операции-погашения
            // останутся в отчётах как реальные доходы/расходы, а остаток долга будет потерян.
            var repaidAmount = await context.DebtPayments
                .AsNoTracking()
                .Where(dp => dp.DebtTransactionId == debtTransactionId && !dp.RepaymentTransaction.IsDeleted)
                .Select(dp => (double?)dp.Amount)
                .SumAsync(ct)
                .ConfigureAwait(false) ?? 0;

            if (repaidAmount > 0)
                throw new InvalidOperationException("Нельзя списать долг, по которому есть погашения. Удалите погашения перед списанием.");

            debt.DebtStatus = DebtStatus.WrittenOff;
            debt.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Долг ID={DebtId} списан", debtTransactionId);
        }

        /// <summary>
        /// Получает остаток по долгу.
        /// </summary>
        public async Task<decimal> GetRemainingAmountAsync(int debtTransactionId, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var debt = await context.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == debtTransactionId && !t.IsDeleted, ct)
                .ConfigureAwait(false);

            if (debt == null)
                throw new InvalidOperationException($"Долг ID={debtTransactionId} не найден");

            if (debt.PaymentType != PaymentType.Debt)
                throw new InvalidOperationException("Операция не является долгом");

            if (debt.DebtStatus == DebtStatus.WrittenOff)
                return 0m;

            var repaid = await context.DebtPayments
                .AsNoTracking()
                .Where(dp => dp.DebtTransactionId == debtTransactionId && !dp.RepaymentTransaction.IsDeleted)
                .Select(dp => (double?)dp.Amount)
                .SumAsync(ct)
                .ConfigureAwait(false) ?? 0;

            return debt.Amount - (decimal)repaid;
        }

        /// <summary>
        /// Обновляет статус долга после изменения/удаления операции погашения.
        /// </summary>
        public async Task RecalculateStatusAsync(int debtTransactionId, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var debt = await context.Transactions
                .FirstOrDefaultAsync(t => t.Id == debtTransactionId && !t.IsDeleted, ct)
                .ConfigureAwait(false);

            if (debt == null || debt.PaymentType != PaymentType.Debt)
                return;

            if (debt.DebtStatus == DebtStatus.WrittenOff)
                return;

            var repaid = await context.DebtPayments
                .Where(dp => dp.DebtTransactionId == debtTransactionId && !dp.RepaymentTransaction.IsDeleted)
                .Select(dp => (double?)dp.Amount)
                .SumAsync(ct)
                .ConfigureAwait(false) ?? 0;

            var previousStatus = debt.DebtStatus;
            debt.DebtStatus = repaid >= (double)debt.Amount ? DebtStatus.Repaid : DebtStatus.Active;
            debt.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            if (previousStatus != debt.DebtStatus)
            {
                _log.Information(
                    "Статус долга ID={DebtId} изменён с {OldStatus} на {NewStatus}",
                    debtTransactionId, previousStatus, debt.DebtStatus);
            }
        }
    }
}
