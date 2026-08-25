using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с финансовыми операциями
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private static readonly ILogger _log = Log.ForContext<TransactionService>();

        /// <summary>
        /// SQL полнотекстового поиска по описанию через FTS5-индекс TransactionsFts
        /// (создаётся миграцией AddTransactionsFts). Параметр {0} — MATCH-выражение.
        /// </summary>
        private const string FtsSearchSql =
            "SELECT * FROM \"Transactions\" WHERE \"Id\" IN (SELECT rowid FROM \"TransactionsFts\" WHERE \"TransactionsFts\" MATCH {0})";

        /// <summary>
        /// Максимальная длина описания операции — соответствует [MaxLength(500)] в Models/Transaction.cs.
        /// </summary>
        private const int MaxDescriptionLength = 500;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TransactionService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Получить список операций с фильтрацией.
        /// Поиск по <see cref="TransactionFilterParameters.SearchText"/> выполняется через
        /// полнотекстовый индекс SQLite FTS5: регистронезависимый (включая кириллицу),
        /// по префиксам слов (например, «рем» найдёт «ремонт»), слова объединяются по И.
        /// Поиск подстроки в середине слова («ожо» → «похоже») больше не срабатывает.
        /// </summary>
        public async Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var hasSearch = !string.IsNullOrWhiteSpace(filters.SearchText);
            var matchQuery = hasSearch ? BuildFtsMatchQuery(filters.SearchText) : null;

            // Поисковый текст задан, но не содержит слов (только знаки препинания) —
            // совпадений быть не может, к базе не обращаемся.
            if (hasSearch && matchQuery == null)
                return new List<Transaction>();

            // FromSqlRaw должен быть начальной точкой запроса (нельзя применить Include до него),
            // поэтому базовый запрос выбирается в зависимости от наличия поиска.
            IQueryable<Transaction> query = matchQuery != null
                ? context.Transactions.FromSqlRaw(FtsSearchSql, matchQuery)
                : context.Transactions;

            query = query
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.CreditCard)
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);
            query = ApplySorting(query, filters.SortBy, filters.SortDescending);

            query = query.Skip(filters.Skip).Take(filters.Take);
            return await query.ToListAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Получить общее количество операций с учетом фильтров.
        /// При поиске по тексту возвращает точное количество совпадений FTS5,
        /// поэтому пагинация корректна на любых объёмах данных.
        /// </summary>
        public async Task<int> GetTotalCountAsync(TransactionFilterParameters filters, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            var hasSearch = !string.IsNullOrWhiteSpace(filters.SearchText);
            var matchQuery = hasSearch ? BuildFtsMatchQuery(filters.SearchText) : null;

            if (hasSearch && matchQuery == null)
                return 0;

            IQueryable<Transaction> query = matchQuery != null
                ? context.Transactions.FromSqlRaw(FtsSearchSql, matchQuery)
                : context.Transactions;

            query = query
                .AsNoTracking()
                .Where(t => !t.IsDeleted);

            query = ApplyFilters(query, filters);

            return await query.CountAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Получить операцию по ID (tracked, для редактирования)
        /// </summary>
        public async Task<Transaction> GetByIdAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            return await context.Transactions
                .Include(t => t.Category)
                .Include(t => t.CreditCard)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Добавить новую операцию
        /// </summary>
        public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken ct = default)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма операции должна быть больше нуля", nameof(transaction));

            if (transaction.Description?.Length > MaxDescriptionLength)
                throw new ArgumentException($"Описание не должно превышать {MaxDescriptionLength} символов", nameof(transaction));

            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbTransaction = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var category = await context.Categories
                .FirstOrDefaultAsync(c => c.Id == transaction.CategoryId, ct)
                .ConfigureAwait(false);
            if (category == null || !category.IsActive)
                throw new InvalidOperationException($"Категория ID={transaction.CategoryId} не найдена или неактивна");

            var isCreditCardRepayment = category.Name == CreditCardService.RepaymentCategoryName
                                        && transaction.CreditCardId.HasValue;

            // Погашение кредитной карты создаётся в том же контексте и транзакции,
            // чтобы операция и распределение по покупкам были атомарны.
            if (isCreditCardRepayment)
            {
                var repayment = await CreateCreditCardRepaymentInContextAsync(context, transaction, ct).ConfigureAwait(false);
                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

                _log.Information("Добавлена операция-погашение кредитной карты ID={Id}, сумма={Amount}", repayment.Id, repayment.Amount);
                return repayment;
            }

            transaction.CreatedAt = DateTime.Now;
            transaction.IsDeleted = false;

            // Обработка долговой операции
            if (transaction.PaymentType == PaymentType.Debt)
            {
                if (!transaction.CounterpartyId.HasValue)
                    throw new InvalidOperationException("Для долговой операции необходимо выбрать контрагента");

                transaction.DebtStatus = DebtStatus.Active;
            }
            else
            {
                transaction.DebtStatus = DebtStatus.NotDebt;
            }

            // Отключаем навигационные свойства, полученные из другого DbContext,
            // чтобы избежать конфликтов отслеживания.
            transaction.Category = null;
            transaction.CreditCard = null;
            transaction.Counterparty = null;

            context.Transactions.Add(transaction);
            transaction.Category = category;
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // Обработка операций по кредитной карте выполняется в той же транзакции,
            // что и сама операция. Доход на карту — пополнение: уменьшает долг и
            // распределяется по покупкам FIFO. Расход — покупка: проверка лимита
            // и создание записи CreditCardPurchase.
            if (transaction.Type == OperationType.Income && transaction.CreditCardId.HasValue)
            {
                await RegisterCreditCardReplenishmentInContextAsync(context, transaction, ct).ConfigureAwait(false);
            }
            else if (transaction.PaymentType == PaymentType.CreditCard && transaction.CreditCardId.HasValue)
            {
                await CreditCardOperationHelper.AddPurchaseAsync(
                    context,
                    transaction.CreditCardId.Value,
                    transaction.Id,
                    transaction.Amount,
                    ct).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

            _log.Information("Добавлена операция ID={Id}, тип={Type}, сумма={Amount}", transaction.Id, transaction.Type, transaction.Amount);
            return transaction;
        }

        /// <summary>
        /// Обновить операцию
        /// </summary>
        public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма операции должна быть больше нуля", nameof(transaction));

            if (transaction.Description?.Length > MaxDescriptionLength)
                throw new ArgumentException($"Описание не должно превышать {MaxDescriptionLength} символов", nameof(transaction));

            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbTransaction = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var categoryExists = await context.Categories
                .AnyAsync(c => c.Id == transaction.CategoryId && c.IsActive, ct)
                .ConfigureAwait(false);
            if (!categoryExists)
                throw new InvalidOperationException($"Категория ID={transaction.CategoryId} не найдена или неактивна");

            // Каждый вызов использует свой DbContext (unit of work), поэтому конфликтов
            // отслеживания быть не может. Загружаем сущность явно и обновляем поля.
            var existing = await context.Transactions.FindAsync(new object[] { transaction.Id }, ct).ConfigureAwait(false);
            if (existing == null)
                throw new InvalidOperationException($"Операция ID={transaction.Id} не найдена");

            var isDebtRepayment = await context.DebtPayments
                .AnyAsync(dp => dp.RepaymentTransactionId == transaction.Id, ct)
                .ConfigureAwait(false);
            var isCreditCardRepayment = await context.CreditCardPayments
                .AnyAsync(ccp => ccp.RepaymentTransactionId == transaction.Id, ct)
                .ConfigureAwait(false);
            if (isDebtRepayment || isCreditCardRepayment)
                throw new InvalidOperationException("Редактирование операции-погашения запрещено");

            var oldPaymentType = existing.PaymentType;
            var oldCreditCardId = existing.CreditCardId;
            var oldCategoryId = existing.CategoryId;
            var oldAmount = existing.Amount;

            existing.Date = transaction.Date;
            existing.Amount = transaction.Amount;
            existing.Type = transaction.Type;
            existing.PaymentType = transaction.PaymentType;
            existing.CategoryId = transaction.CategoryId;
            existing.CreditCardId = transaction.CreditCardId;
            existing.CounterpartyId = transaction.CounterpartyId;
            existing.Description = transaction.Description;
            existing.UpdatedAt = DateTime.Now;

            // Обработка долговой операции
            if (existing.PaymentType == PaymentType.Debt)
            {
                if (!existing.CounterpartyId.HasValue)
                    throw new InvalidOperationException("Для долговой операции необходимо выбрать контрагента");

                // Если операция ранее не была долгом — устанавливаем статус Active
                if (existing.DebtStatus == DebtStatus.NotDebt)
                    existing.DebtStatus = DebtStatus.Active;
            }
            else
            {
                existing.DebtStatus = DebtStatus.NotDebt;
                existing.CounterpartyId = null;

                // При смене типа операции с долга на обычную удаляем связи с погашениями
                var debtPayments = await context.DebtPayments
                    .Where(dp => dp.DebtTransactionId == existing.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                if (debtPayments.Count > 0)
                    context.DebtPayments.RemoveRange(debtPayments);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // Обновляем навигационное свойство
            await context.Entry(existing).Reference(t => t.Category).LoadAsync(ct).ConfigureAwait(false);

            // Обработка изменений, связанных с кредитной картой
            await HandleCreditCardChangesAsync(
                context,
                existing,
                oldPaymentType,
                oldCreditCardId,
                oldCategoryId,
                oldAmount,
                ct).ConfigureAwait(false);

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

            _log.Information("Обновлена операция ID={Id}, сумма={Amount}", transaction.Id, transaction.Amount);
        }

        /// <summary>
        /// Удалить операцию (soft delete)
        /// </summary>
        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbTransaction = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var transaction = await context.Transactions.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
            if (transaction == null)
                return;

            transaction.IsDeleted = true;
            transaction.UpdatedAt = DateTime.Now;

            // При удалении операции по кредитной карте удаляем связанную покупку.
            // Связанные CreditCardPayment удаляются каскадно.
            if (transaction.PaymentType == PaymentType.CreditCard)
            {
                var purchase = await context.CreditCardPurchases
                    .FirstOrDefaultAsync(p => p.TransactionId == id, ct)
                    .ConfigureAwait(false);
                if (purchase != null)
                {
                    context.CreditCardPurchases.Remove(purchase);
                }
            }

            int? relatedDebtId = null;

            // При удалении операции-погашения кредитной карты восстанавливаем остатки покупок
            if (transaction.PaymentType != PaymentType.Debt)
            {
                var creditCardPayment = await context.CreditCardPayments
                    .FirstOrDefaultAsync(ccp => ccp.RepaymentTransactionId == id, ct)
                    .ConfigureAwait(false);

                if (creditCardPayment != null)
                {
                    await CreditCardOperationHelper.RecalculateOnRepaymentDeletedAsync(
                        context, id, ct).ConfigureAwait(false);
                }
            }

            // При удалении долговой операции делаем soft delete связанных операций-погашений.
            // Сами записи DebtPayment оставляем в БД для корректного восстановления через RestoreAsync.
            if (transaction.PaymentType == PaymentType.Debt)
            {
                var debtPayments = await context.DebtPayments
                    .Where(dp => dp.DebtTransactionId == id)
                    .Include(dp => dp.RepaymentTransaction)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var debtPayment in debtPayments)
                {
                    if (debtPayment.RepaymentTransaction != null)
                    {
                        debtPayment.RepaymentTransaction.IsDeleted = true;
                        debtPayment.RepaymentTransaction.UpdatedAt = DateTime.Now;
                    }
                }
            }
            else
            {
                // Проверяем, является ли удаляемая операция погашением долга
                var debtPayment = await context.DebtPayments
                    .FirstOrDefaultAsync(dp => dp.RepaymentTransactionId == id, ct)
                    .ConfigureAwait(false);

                if (debtPayment != null)
                {
                    relatedDebtId = debtPayment.DebtTransactionId;
                }
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // Если удалили погашение — пересчитываем статус связанного долга в том же контексте
            if (relatedDebtId.HasValue)
            {
                await RecalculateDebtStatusInContextAsync(context, relatedDebtId.Value, ct).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

            _log.Information("Удалена операция ID={Id}", id);
        }

        /// <summary>
        /// Удалить несколько операций одним контекстом и одной транзакцией БД (soft delete).
        /// Бизнес-правила на каждую операцию — те же, что в <see cref="DeleteAsync"/>:
        /// жёсткое удаление покупок по кредитным картам, восстановление остатков покупок
        /// при удалении погашений кредитки, soft delete погашений удаляемых долгов
        /// и пересчёт статусов связанных долгов.
        /// </summary>
        public async Task DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var idList = ids?.Distinct().ToList() ?? new List<int>();
            if (idList.Count == 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbTransaction = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var transactions = await context.Transactions
                .Where(t => idList.Contains(t.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (transactions.Count == 0)
                return;

            var now = DateTime.Now;

            foreach (var transaction in transactions)
            {
                transaction.IsDeleted = true;
                transaction.UpdatedAt = now;
            }

            // При удалении операций по кредитной карте удаляем связанные покупки.
            // Связанные CreditCardPayment удаляются каскадно.
            var creditCardTransactionIds = transactions
                .Where(t => t.PaymentType == PaymentType.CreditCard)
                .Select(t => t.Id)
                .ToList();
            if (creditCardTransactionIds.Count > 0)
            {
                var purchases = await context.CreditCardPurchases
                    .Where(p => creditCardTransactionIds.Contains(p.TransactionId))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
                context.CreditCardPurchases.RemoveRange(purchases);
            }

            var nonDebtIds = transactions
                .Where(t => t.PaymentType != PaymentType.Debt)
                .Select(t => t.Id)
                .ToList();

            // При удалении операций-погашений кредитной карты восстанавливаем остатки покупок
            if (nonDebtIds.Count > 0)
            {
                var creditCardPayments = await context.CreditCardPayments
                    .Where(ccp => nonDebtIds.Contains(ccp.RepaymentTransactionId))
                    .Select(ccp => ccp.RepaymentTransactionId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var repaymentId in creditCardPayments)
                {
                    await CreditCardOperationHelper.RecalculateOnRepaymentDeletedAsync(
                        context, repaymentId, ct).ConfigureAwait(false);
                }
            }

            // При удалении долговых операций делаем soft delete связанных операций-погашений.
            // Сами записи DebtPayment оставляем в БД для корректного восстановления через RestoreAsync.
            var debtIds = transactions
                .Where(t => t.PaymentType == PaymentType.Debt)
                .Select(t => t.Id)
                .ToList();
            if (debtIds.Count > 0)
            {
                var debtPayments = await context.DebtPayments
                    .Where(dp => debtIds.Contains(dp.DebtTransactionId))
                    .Include(dp => dp.RepaymentTransaction)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var debtPayment in debtPayments)
                {
                    if (debtPayment.RepaymentTransaction != null && !debtPayment.RepaymentTransaction.IsDeleted)
                    {
                        debtPayment.RepaymentTransaction.IsDeleted = true;
                        debtPayment.RepaymentTransaction.UpdatedAt = now;
                    }
                }
            }

            // Проверяем, какие из удаляемых операций являются погашениями долгов,
            // и запоминаем связанные долги для пересчёта их статусов.
            var relatedDebtIds = new HashSet<int>();
            if (nonDebtIds.Count > 0)
            {
                var repaymentLinks = await context.DebtPayments
                    .Where(dp => nonDebtIds.Contains(dp.RepaymentTransactionId))
                    .Select(dp => dp.DebtTransactionId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var debtId in repaymentLinks)
                    relatedDebtIds.Add(debtId);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // Если удалили погашения — пересчитываем статусы связанных долгов в том же контексте
            foreach (var debtId in relatedDebtIds)
            {
                await RecalculateDebtStatusInContextAsync(context, debtId, ct).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

            _log.Information("Удалено операций: {Count}", transactions.Count);
        }

        /// <summary>
        /// Восстановить ранее удалённую операцию (отмена soft delete).
        /// Атомарно восстанавливает связанные сущности: кредитные покупки,
        /// погашения долгов и погашения кредитных карт.
        /// </summary>
        public async Task RestoreAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbTransaction = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var transaction = await context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted, ct)
                .ConfigureAwait(false);

            if (transaction == null)
                return;

            // 1. Восстановление кредитной покупки.
            // При удалении покупки CreditCardPurchase удалялась жёстко,
            // поэтому при восстановлении операции создаём её заново.
            // Доход на карту (пополнение) покупку не создаёт — он обрабатывается ниже.
            if (transaction.Type == OperationType.Expense
                && transaction.PaymentType == PaymentType.CreditCard
                && transaction.CreditCardId.HasValue)
            {
                var purchaseExists = await context.CreditCardPurchases
                    .AnyAsync(p => p.TransactionId == id, ct)
                    .ConfigureAwait(false);

                if (!purchaseExists)
                {
                    context.CreditCardPurchases.Add(new CreditCardPurchase
                    {
                        CreditCardId = transaction.CreditCardId.Value,
                        TransactionId = transaction.Id,
                        Amount = transaction.Amount,
                        RemainingDebt = transaction.Amount,
                        PurchaseDate = transaction.Date,
                        Notes = transaction.Description
                    });
                }
            }

            // Восстанавливаем саму операцию до пересчёта статусов,
            // чтобы связанные погашения корректно учитывались в расчётах.
            transaction.IsDeleted = false;
            transaction.UpdatedAt = DateTime.Now;

            // Ссылка на долг, если восстанавливаемая операция — погашение долга
            // (кэшируем, чтобы не повторять запрос при пересчёте статусов ниже).
            DebtPayment debtRepaymentLink = null;

            // 2. Восстановление долговой операции и связанных с ней погашений.
            if (transaction.PaymentType == PaymentType.Debt)
            {
                var debtPayments = await context.DebtPayments
                    .Where(dp => dp.DebtTransactionId == id)
                    .Include(dp => dp.RepaymentTransaction)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var debtPayment in debtPayments)
                {
                    if (debtPayment.RepaymentTransaction != null)
                    {
                        debtPayment.RepaymentTransaction.IsDeleted = false;
                        debtPayment.RepaymentTransaction.UpdatedAt = DateTime.Now;
                    }
                }
            }
            else
            {
                // 3. Восстановление операции-погашения кредитной карты или пополнения карты.
                // Пополнение (доход на карту) при восстановлении заново распределяется
                // по покупкам FIFO так же, как погашение.
                var isCreditCardRepayment = transaction.CreditCardId.HasValue
                                            && ((transaction.Type == OperationType.Expense
                                                 && transaction.Category?.Name == CreditCardService.RepaymentCategoryName)
                                                || transaction.Type == OperationType.Income);

                if (isCreditCardRepayment)
                {
                    var creditCardPaymentExists = await context.CreditCardPayments
                        .AnyAsync(ccp => ccp.RepaymentTransactionId == id, ct)
                        .ConfigureAwait(false);

                    if (!creditCardPaymentExists)
                    {
                        var card = await context.CreditCards
                            .FindAsync(new object[] { transaction.CreditCardId.Value }, ct)
                            .ConfigureAwait(false);

                        if (card != null)
                        {
                            var currentDebt = await CreditCardOperationHelper.GetCurrentDebtAsync(
                                context, transaction.CreditCardId.Value, ct: ct).ConfigureAwait(false);

                            if (transaction.Amount > currentDebt)
                                throw new InvalidOperationException(
                                    transaction.Type == OperationType.Income
                                        ? "Сумма пополнения превышает текущий долг по карте"
                                        : "Сумма погашения превышает текущий долг по карте");

                            card.LastPaymentDate = DateTime.Today;
                            await CreditCardOperationHelper.DistributeRepaymentAsync(
                                context, id, transaction.CreditCardId.Value, transaction.Amount, ct).ConfigureAwait(false);
                        }
                    }
                }

                // 4. Определяем, является ли операция погашением долга.
                debtRepaymentLink = await context.DebtPayments
                    .FirstOrDefaultAsync(dp => dp.RepaymentTransactionId == id, ct)
                    .ConfigureAwait(false);
            }

            // Сохраняем восстановление операции и связей, чтобы последующий
            // пересчёт статусов долгов работал с актуальным состоянием БД.
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            // 5. Пересчёт статусов связанных долгов после восстановления погашений.
            if (transaction.PaymentType == PaymentType.Debt)
            {
                await RecalculateDebtStatusInContextAsync(context, id, ct).ConfigureAwait(false);
            }
            else if (debtRepaymentLink != null)
            {
                await RecalculateDebtStatusInContextAsync(context, debtRepaymentLink.DebtTransactionId, ct).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

            _log.Information("Восстановлена операция ID={Id}", id);
        }

        /// <summary>
        /// Восстановить несколько ранее удалённых операций одним контекстом и одной транзакцией БД.
        /// Бизнес-правила на каждую операцию — те же, что в <see cref="RestoreAsync"/>:
        /// пересоздание покупок по кредитным картам, FIFO-распределение погашений кредитки,
        /// восстановление погашений долгов. Статусы связанных долгов пересчитываются
        /// по одному разу на долг после восстановления всех операций.
        /// </summary>
        public async Task RestoreRangeAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var idList = ids?.Distinct().ToList() ?? new List<int>();
            if (idList.Count == 0)
                return;

            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbTransaction = await context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var transactions = await context.Transactions
                .Include(t => t.Category)
                .Where(t => idList.Contains(t.Id) && t.IsDeleted)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (transactions.Count == 0)
                return;

            var now = DateTime.Now;
            var restoredCount = 0;

            // Статусы долгов пересчитываем один раз на долг после восстановления всех операций
            var debtIdsToRecalculate = new HashSet<int>();

            foreach (var transaction in transactions)
            {
                // Операция могла быть восстановлена неявно на предыдущей итерации
                // (как погашение восстановленного долга) — пропускаем.
                if (!transaction.IsDeleted)
                    continue;

                // 1. Восстановление кредитной покупки.
                // При удалении покупки CreditCardPurchase удалялась жёстко,
                // поэтому при восстановлении операции создаём её заново.
                // Доход на карту (пополнение) покупку не создаёт — он обрабатывается ниже.
                if (transaction.Type == OperationType.Expense
                    && transaction.PaymentType == PaymentType.CreditCard
                    && transaction.CreditCardId.HasValue)
                {
                    var purchaseExists = await context.CreditCardPurchases
                        .AnyAsync(p => p.TransactionId == transaction.Id, ct)
                        .ConfigureAwait(false);

                    if (!purchaseExists)
                    {
                        context.CreditCardPurchases.Add(new CreditCardPurchase
                        {
                            CreditCardId = transaction.CreditCardId.Value,
                            TransactionId = transaction.Id,
                            Amount = transaction.Amount,
                            RemainingDebt = transaction.Amount,
                            PurchaseDate = transaction.Date,
                            Notes = transaction.Description
                        });
                    }
                }

                // Восстанавливаем саму операцию до пересчёта статусов,
                // чтобы связанные погашения корректно учитывались в расчётах.
                transaction.IsDeleted = false;
                transaction.UpdatedAt = now;

                // 2. Восстановление долговой операции и связанных с ней погашений.
                if (transaction.PaymentType == PaymentType.Debt)
                {
                    var debtPayments = await context.DebtPayments
                        .Where(dp => dp.DebtTransactionId == transaction.Id)
                        .Include(dp => dp.RepaymentTransaction)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    foreach (var debtPayment in debtPayments)
                    {
                        if (debtPayment.RepaymentTransaction != null && debtPayment.RepaymentTransaction.IsDeleted)
                        {
                            debtPayment.RepaymentTransaction.IsDeleted = false;
                            debtPayment.RepaymentTransaction.UpdatedAt = now;
                        }
                    }

                    debtIdsToRecalculate.Add(transaction.Id);
                }
                else
                {
                    // 3. Восстановление операции-погашения кредитной карты или пополнения карты.
                    // Пополнение (доход на карту) при восстановлении заново распределяется
                    // по покупкам FIFO так же, как погашение.
                    var isCreditCardRepayment = transaction.CreditCardId.HasValue
                                                && ((transaction.Type == OperationType.Expense
                                                     && transaction.Category?.Name == CreditCardService.RepaymentCategoryName)
                                                    || transaction.Type == OperationType.Income);

                    if (isCreditCardRepayment)
                    {
                        var creditCardPaymentExists = await context.CreditCardPayments
                            .AnyAsync(ccp => ccp.RepaymentTransactionId == transaction.Id, ct)
                            .ConfigureAwait(false);

                        if (!creditCardPaymentExists)
                        {
                            var card = await context.CreditCards
                                .FindAsync(new object[] { transaction.CreditCardId.Value }, ct)
                                .ConfigureAwait(false);

                            if (card != null)
                            {
                                var currentDebt = await CreditCardOperationHelper.GetCurrentDebtAsync(
                                    context, transaction.CreditCardId.Value, ct: ct).ConfigureAwait(false);

                                if (transaction.Amount > currentDebt)
                                    throw new InvalidOperationException(
                                        transaction.Type == OperationType.Income
                                            ? "Сумма пополнения превышает текущий долг по карте"
                                            : "Сумма погашения превышает текущий долг по карте");

                                card.LastPaymentDate = DateTime.Today;
                                await CreditCardOperationHelper.DistributeRepaymentAsync(
                                    context, transaction.Id, transaction.CreditCardId.Value, transaction.Amount, ct).ConfigureAwait(false);
                            }
                        }
                    }

                    // 4. Определяем, является ли операция погашением долга.
                    var debtRepaymentLink = await context.DebtPayments
                        .FirstOrDefaultAsync(dp => dp.RepaymentTransactionId == transaction.Id, ct)
                        .ConfigureAwait(false);

                    if (debtRepaymentLink != null)
                        debtIdsToRecalculate.Add(debtRepaymentLink.DebtTransactionId);
                }

                // Сохраняем после каждой операции, чтобы последующие операции пакета
                // (проверка текущего долга карты, FIFO-распределение) видели
                // уже восстановленные покупки и погашения.
                await context.SaveChangesAsync(ct).ConfigureAwait(false);
                restoredCount++;
            }

            // 5. Пересчёт статусов связанных долгов после восстановления — по одному разу на долг.
            foreach (var debtId in debtIdsToRecalculate)
            {
                await RecalculateDebtStatusInContextAsync(context, debtId, ct).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);

            _log.Information("Восстановлено операций: {Count}", restoredCount);
        }

        /// <summary>
        /// Проверяет, является ли операция погашением долга или погашением кредитной карты.
        /// </summary>
        public async Task<bool> IsRepaymentTransactionAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var isDebtRepayment = await context.DebtPayments
                .AnyAsync(dp => dp.RepaymentTransactionId == id, ct)
                .ConfigureAwait(false);
            var isCreditCardRepayment = await context.CreditCardPayments
                .AnyAsync(ccp => ccp.RepaymentTransactionId == id, ct)
                .ConfigureAwait(false);
            return isDebtRepayment || isCreditCardRepayment;
        }

        /// <summary>
        /// Получить последние N операций
        /// </summary>
        public async Task<List<Transaction>> GetRecentAsync(int count = 10, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            return await context.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Include(t => t.CreditCard)
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Суммарные доходы/расходы за период — SQL GROUP BY, без загрузки всех записей
        /// </summary>
        public async Task<(decimal Income, decimal Expense, int IncomeCount, int ExpenseCount)> GetPeriodTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var query = context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var incomeRow  = rows.FirstOrDefault(r => r.Type == OperationType.Income);
            var expenseRow = rows.FirstOrDefault(r => r.Type == OperationType.Expense);
            return (
                incomeRow?.Total  ?? 0m,
                expenseRow?.Total ?? 0m,
                incomeRow?.Count  ?? 0,
                expenseRow?.Count ?? 0);
        }

        /// <summary>
        /// Суммы доходов/расходов за период по каждому типу оплаты — один SQL GROUP BY
        /// </summary>
        public async Task<List<(PaymentType PaymentType, decimal Income, decimal Expense)>> GetPeriodTotalsByPaymentTypeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var rows = await context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo)
                .GroupBy(t => new { t.PaymentType, t.Type })
                .Select(g => new { g.Key.PaymentType, g.Key.Type, Total = g.Sum(t => t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return rows
                .GroupBy(r => r.PaymentType)
                .Select(g => (
                    PaymentType: g.Key,
                    Income: g.FirstOrDefault(r => r.Type == OperationType.Income)?.Total ?? 0m,
                    Expense: g.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m))
                .ToList();
        }

        /// <summary>
        /// Дневные итоги за период — SQL GROUP BY date, для графиков и группировки
        /// </summary>
        public async Task<List<DailyTotalsItem>> GetDailyTotalsAsync(DateTime from, DateTime to, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var query = context.Transactions
                .Where(t => !t.IsDeleted && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Date.Date)
                .Select(g => new
                {
                    Date    = g.Key,
                    Income  = g.Sum(t => t.Type == OperationType.Income ? t.Amount : 0m),
                    Expense = g.Sum(t => t.Type == OperationType.Expense ? t.Amount : 0m)
                })
                .OrderBy(x => x.Date)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return rows.Select(r => new DailyTotalsItem
            {
                Date    = r.Date,
                Income  = r.Income,
                Expense = r.Expense
            }).ToList();
        }

        /// <summary>
        /// Топ N категорий по сумме для заданного типа операции
        /// </summary>
        public async Task<List<(string Name, decimal Total)>> GetTopCategoriesAsync(
            DateTime from, DateTime to, OperationType type, int count, PaymentType? paymentType = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var dateTo = to.Date.AddDays(1);
            var query = context.Transactions
                .AsNoTracking()
                .Where(t => !t.IsDeleted && t.Type == type && t.Date >= from && t.Date < dateTo);
            if (paymentType.HasValue)
                query = query.Where(t => t.PaymentType == paymentType.Value);
            var rows = await query
                .GroupBy(t => t.Category.Name ?? "?")
                .Select(g => new { Name = g.Key, Total = g.Sum(t => t.Amount) })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return rows
                .OrderByDescending(x => x.Total)
                .Take(count)
                .Select(r => (r.Name, r.Total))
                .ToList();
        }

        #region Вспомогательные методы

        /// <summary>
        /// Создать операцию-погашение кредитной карты в рамках переданного контекста.
        /// </summary>
        private async Task<Transaction> CreateCreditCardRepaymentInContextAsync(AppDbContext context, Transaction transaction, CancellationToken ct = default)
        {
            if (transaction.Amount <= 0)
                throw new ArgumentException("Сумма операции должна быть больше нуля", nameof(transaction));

            if (transaction.Description?.Length > MaxDescriptionLength)
                throw new ArgumentException($"Описание не должно превышать {MaxDescriptionLength} символов", nameof(transaction));

            if (!transaction.CreditCardId.HasValue)
                throw new InvalidOperationException("Для погашения кредитной карты необходимо выбрать карту");

            if (transaction.PaymentType != PaymentType.Cash && transaction.PaymentType != PaymentType.NonCash)
                throw new ArgumentException("Погашение кредитной карты возможно только наличными или безналичными", nameof(transaction.PaymentType));

            var card = await context.CreditCards
                .FindAsync(new object[] { transaction.CreditCardId.Value }, ct)
                .ConfigureAwait(false);

            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={transaction.CreditCardId.Value} не найдена");

            if (!card.IsActive)
                throw new InvalidOperationException("Нельзя погасить неактивную карту");

            var repaymentCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == CreditCardService.RepaymentCategoryName && c.Type == OperationType.Expense, ct)
                .ConfigureAwait(false);

            if (repaymentCategory == null)
                throw new InvalidOperationException($"Системная категория \"{CreditCardService.RepaymentCategoryName}\" не найдена");

            var currentDebt = await CreditCardOperationHelper.GetCurrentDebtAsync(
                context, card.Id, ct: ct).ConfigureAwait(false);

            if (transaction.Amount > currentDebt)
                throw new InvalidOperationException("Сумма погашения превышает текущий долг по карте");

            transaction.Type = OperationType.Expense;
            transaction.CategoryId = repaymentCategory.Id;
            transaction.DebtStatus = DebtStatus.NotDebt;
            transaction.CreatedAt = DateTime.Now;
            transaction.IsDeleted = false;
            transaction.CounterpartyId = null;

            // Отключаем навигационные свойства, чтобы не возникало конфликтов
            // отслеживания при передаче сущности из другого DbContext.
            transaction.Category = null;
            transaction.CreditCard = null;
            transaction.Counterparty = null;

            context.Transactions.Add(transaction);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            card.LastPaymentDate = DateTime.Today;
            await CreditCardOperationHelper.DistributeRepaymentAsync(
                context, transaction.Id, card.Id, transaction.Amount, ct).ConfigureAwait(false);

            // Восстанавливаем навигационное свойство для вызывающего кода.
            transaction.Category = repaymentCategory;

            return transaction;
        }

        /// <summary>
        /// Зарегистрировать пополнение кредитной карты (доход на карту) в рамках переданного контекста.
        /// Операция уже сохранена и имеет Id. Пополнение уменьшает долг по карте и распределяется
        /// по покупкам FIFO (как погашение), но не является расходом собственных средств
        /// и не создаёт запись покупки.
        /// </summary>
        private async Task RegisterCreditCardReplenishmentInContextAsync(AppDbContext context, Transaction transaction, CancellationToken ct = default)
        {
            var card = await context.CreditCards
                .FindAsync(new object[] { transaction.CreditCardId.Value }, ct)
                .ConfigureAwait(false);

            if (card == null)
                throw new InvalidOperationException($"Кредитная карта ID={transaction.CreditCardId.Value} не найдена");

            if (!card.IsActive)
                throw new InvalidOperationException("Нельзя пополнить неактивную карту");

            var currentDebt = await CreditCardOperationHelper.GetCurrentDebtAsync(
                context, card.Id, ct: ct).ConfigureAwait(false);

            if (transaction.Amount > currentDebt)
                throw new InvalidOperationException("Сумма пополнения превышает текущий долг по карте");

            // DistributeRepaymentAsync также обновляет LastPaymentDate карты
            await CreditCardOperationHelper.DistributeRepaymentAsync(
                context, transaction.Id, card.Id, transaction.Amount, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Пересчитать статус долга в рамках переданного DbContext.
        /// </summary>
        private async Task RecalculateDebtStatusInContextAsync(AppDbContext context, int debtTransactionId, CancellationToken ct = default)
        {
            var debt = await context.Transactions
                .FirstOrDefaultAsync(t => t.Id == debtTransactionId && !t.IsDeleted, ct)
                .ConfigureAwait(false);

            if (debt == null || debt.PaymentType != PaymentType.Debt)
                return;

            if (debt.DebtStatus == DebtStatus.WrittenOff)
                return;

            var repaid = await context.DebtPayments
                .Where(dp => dp.DebtTransactionId == debtTransactionId && !dp.RepaymentTransaction.IsDeleted)
                .SumAsync(dp => (decimal?)dp.Amount, ct)
                .ConfigureAwait(false) ?? 0m;

            var previousStatus = debt.DebtStatus;
            debt.DebtStatus = repaid >= debt.Amount ? DebtStatus.Repaid : DebtStatus.Active;
            debt.UpdatedAt = DateTime.Now;

            if (previousStatus != debt.DebtStatus)
            {
                _log.Information(
                    "Статус долга ID={DebtId} изменён с {OldStatus} на {NewStatus}",
                    debtTransactionId, previousStatus, debt.DebtStatus);
            }
        }

        /// <summary>
        /// Обработать изменения, связанные с кредитной картой, при обновлении операции
        /// </summary>
        private async Task HandleCreditCardChangesAsync(
            AppDbContext context,
            Transaction transaction,
            PaymentType oldPaymentType,
            int? oldCreditCardId,
            int oldCategoryId,
            decimal oldAmount,
            CancellationToken ct = default)
        {
            var currentCategory = await context.Categories.FindAsync(new object[] { transaction.CategoryId }, ct).ConfigureAwait(false);
            var oldCategory = await context.Categories.FindAsync(new object[] { oldCategoryId }, ct).ConfigureAwait(false);
            var isRepayment = currentCategory?.Name == CreditCardService.RepaymentCategoryName;
            var wasRepayment = oldCategory?.Name == CreditCardService.RepaymentCategoryName;

            // Удаляем связанную покупку, если операция перестала быть кредитной покупкой:
            // сменился тип оплаты, потеряна карта или тип сменился на доход
            // (доход на карту — пополнение, покупки не создаёт).
            // Если операция одновременно становится погашением по той же карте,
            // покупку необходимо сохранить, чтобы на неё могло быть распределено погашение.
            var becomesRepayment = isRepayment && transaction.CreditCardId.HasValue;
            var keepPurchaseForRepayment = becomesRepayment && oldCreditCardId == transaction.CreditCardId;
            var isCreditPurchase = transaction.Type == OperationType.Expense
                                   && transaction.PaymentType == PaymentType.CreditCard
                                   && transaction.CreditCardId.HasValue;

            if (oldPaymentType == PaymentType.CreditCard &&
                !isCreditPurchase &&
                !keepPurchaseForRepayment)
            {
                var purchase = await context.CreditCardPurchases
                    .FirstOrDefaultAsync(p => p.TransactionId == transaction.Id, ct)
                    .ConfigureAwait(false);
                if (purchase != null)
                {
                    context.CreditCardPurchases.Remove(purchase);
                }
            }

            // Создаём или обновляем покупку, если операция стала/осталась кредитной покупкой.
            // Пополнение (доход на карту) покупку не создаёт: оно уменьшает долг
            // и учитывается в CreditCardOperationHelper.GetCurrentDebtAsync.
            if (isCreditPurchase)
            {
                var purchase = await context.CreditCardPurchases
                    .Include(p => p.CreditCardPayments)
                    .FirstOrDefaultAsync(p => p.TransactionId == transaction.Id, ct)
                    .ConfigureAwait(false);

                if (purchase != null)
                {
                    purchase.CreditCardId = transaction.CreditCardId.Value;
                    purchase.Amount = transaction.Amount;

                    // При изменении суммы покупки оставшийся долг не может быть
                    // меньше суммы уже произведённых погашений.
                    var paid = purchase.CreditCardPayments?.Sum(p => p.Amount) ?? 0m;
                    purchase.RemainingDebt = Math.Max(0m, transaction.Amount - paid);

                    purchase.PurchaseDate = transaction.Date;
                    purchase.Notes = transaction.Description;
                }
                else
                {
                    await CreditCardOperationHelper.AddPurchaseAsync(
                        context,
                        transaction.CreditCardId.Value,
                        transaction.Id,
                        transaction.Amount,
                        ct).ConfigureAwait(false);
                }
            }

            // При смене категории с погашения на обычную восстанавливаем остатки покупок
            if (wasRepayment && !isRepayment && oldCreditCardId.HasValue)
            {
                await CreditCardOperationHelper.RecalculateOnRepaymentDeletedAsync(
                    context, transaction.Id, ct).ConfigureAwait(false);
            }

            // Фиксируем погашение, если категория изменилась на "Погашение кредита"
            if (isRepayment && !wasRepayment && transaction.CreditCardId.HasValue)
            {
                // Операция уже сохранена с категорией погашения, поэтому исключаем её
                // из расчёта текущего долга, чтобы не сравнивать сумму с заниженным остатком.
                var currentDebt = await CreditCardOperationHelper.GetCurrentDebtAsync(
                    context, transaction.CreditCardId.Value, transaction.Id, ct).ConfigureAwait(false);

                if (transaction.Amount > currentDebt)
                    throw new InvalidOperationException("Сумма погашения превышает текущий долг по карте");

                var card = await context.CreditCards
                    .FindAsync(new object[] { transaction.CreditCardId.Value }, ct)
                    .ConfigureAwait(false);
                if (card != null)
                    card.LastPaymentDate = DateTime.Today;

                await CreditCardOperationHelper.DistributeRepaymentAsync(
                    context, transaction.Id, transaction.CreditCardId.Value, transaction.Amount, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Построить MATCH-выражение FTS5 из пользовательского текста.
        /// Токенизатор unicode61 считает словами последовательности букв и цифр,
        /// поэтому запрос приводится к тому же виду: остальные символы — разделители.
        /// Каждое слово ищется по префиксу («рем» → «ремонт»), слова объединяются по И.
        /// Возвращает null, если после очистки не осталось ни одного слова.
        /// </summary>
        private static string BuildFtsMatchQuery(string searchText)
        {
            var chars = searchText
                .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
                .ToArray();
            var tokens = new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
                return null;

            // Токены состоят только из букв и цифр, поэтому спецсимволы MATCH-синтаксиса
            // в них отсутствуют и достаточно обернуть каждый в двойные кавычки.
            // Суффикс * — поиск по префиксу, пробел между фразами — AND.
            // Само выражение передаётся в SQL параметром, интерполяции строк нет.
            return string.Join(" ", tokens.Select(t => $"\"{t}\"*"));
        }

        /// <summary>
        /// Применить фильтры к запросу
        /// </summary>
        private IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> query, TransactionFilterParameters filters)
        {
            // Фильтр по дате
            if (filters.DateFrom.HasValue)
            {
                query = query.Where(t => t.Date >= filters.DateFrom.Value);
            }

            if (filters.DateTo.HasValue)
            {
                // Exclusive upper bound: включаем весь день DateTo
                var dateTo = filters.DateTo.Value.Date.AddDays(1);
                query = query.Where(t => t.Date < dateTo);
            }

            // Фильтр по типу
            if (filters.Type.HasValue)
            {
                query = query.Where(t => t.Type == filters.Type.Value);
            }

            // Фильтр по типу оплаты
            if (filters.PaymentType.HasValue)
            {
                query = query.Where(t => t.PaymentType == filters.PaymentType.Value);
            }

            // Фильтр по категории
            if (filters.CategoryId.HasValue)
            {
                query = query.Where(t => t.CategoryId == filters.CategoryId.Value);
            }

            // Фильтр по контрагенту
            if (filters.CounterpartyId.HasValue)
            {
                query = query.Where(t => t.CounterpartyId == filters.CounterpartyId.Value);
            }

            // Фильтр по кредитной карте: покупки, пополнения и погашения конкретной карты
            if (filters.CreditCardId.HasValue)
            {
                query = query.Where(t => t.CreditCardId == filters.CreditCardId.Value);
            }

            // Фильтр по долгам
            if (filters.IsDebtFilter)
            {
                query = query.Where(t => t.PaymentType == PaymentType.Debt && t.DebtStatus != DebtStatus.NotDebt);
            }
            else
            {
                // По умолчанию операции создания долга скрыты из списка операций;
                // отображаются только обычные операции и операции-погашения.
                query = query.Where(t => t.PaymentType != PaymentType.Debt);
            }

            // Фильтр по статусу долга
            if (filters.DebtStatus.HasValue)
            {
                query = query.Where(t => t.DebtStatus == filters.DebtStatus.Value);
            }

            // Фильтр по сумме
            if (filters.AmountFrom.HasValue)
            {
                query = query.Where(t => t.Amount >= filters.AmountFrom.Value);
            }

            if (filters.AmountTo.HasValue)
            {
                query = query.Where(t => t.Amount <= filters.AmountTo.Value);
            }

            return query;
        }

        /// <summary>
        /// Применить сортировку к запросу
        /// </summary>
        private IQueryable<Transaction> ApplySorting(IQueryable<Transaction> query, string sortBy, bool descending)
        {
            switch (sortBy?.ToLower())
            {
                case "date":
                    query = descending
                        ? query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
                        : query.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt);
                    break;

                case "amount":
                    query = descending
                        ? query.OrderByDescending(t => t.Amount)
                        : query.OrderBy(t => t.Amount);
                    break;

                case "type":
                    query = descending
                        ? query.OrderByDescending(t => t.Type)
                        : query.OrderBy(t => t.Type);
                    break;

                case "category":
                    query = descending
                        ? query.OrderByDescending(t => t.Category.Name)
                        : query.OrderBy(t => t.Category.Name);
                    break;

                default:
                    query = query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt);
                    break;
            }

            return query;
        }

        #endregion
    }
}
