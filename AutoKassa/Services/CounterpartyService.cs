using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы со справочником контрагентов
    /// </summary>
    public class CounterpartyService : ICounterpartyService
    {
        private static readonly ILogger _log = Log.ForContext<CounterpartyService>();

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CounterpartyService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Получить всех контрагентов
        /// </summary>
        public async Task<IReadOnlyList<Counterparty>> GetAllAsync(CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Counterparties
                .AsNoTracking()
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить активных контрагентов с возможностью фильтрации по типу
        /// </summary>
        public async Task<IReadOnlyList<Counterparty>> GetActiveAsync(CounterpartyType? type = null, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var query = context.Counterparties
                .AsNoTracking()
                .Where(c => c.IsActive);

            if (type.HasValue)
                query = query.Where(c => c.Type == type.Value);

            return await query
                .OrderBy(c => c.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Получить контрагента по ID
        /// </summary>
        public async Task<Counterparty?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            return await context.Counterparties
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Добавить нового контрагента
        /// </summary>
        public async Task<Counterparty> AddAsync(Counterparty counterparty, CancellationToken ct = default)
        {
            if (counterparty == null)
                throw new ArgumentNullException(nameof(counterparty));

            if (string.IsNullOrWhiteSpace(counterparty.Name))
                throw new ArgumentException("Название контрагента не может быть пустым", nameof(counterparty));

            if (await ExistsAsync(counterparty.Name, ct).ConfigureAwait(false))
                throw new InvalidOperationException($"Контрагент с названием «{counterparty.Name}» уже существует");

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            counterparty.CreatedAt = DateTime.Now;
            counterparty.IsActive = true;

            context.Counterparties.Add(counterparty);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Добавлен контрагент ID={Id}, название={Name}, тип={Type}", counterparty.Id, counterparty.Name, counterparty.Type);
            return counterparty;
        }

        /// <summary>
        /// Обновить контрагента
        /// </summary>
        public async Task UpdateAsync(Counterparty counterparty, CancellationToken ct = default)
        {
            if (counterparty == null)
                throw new ArgumentNullException(nameof(counterparty));

            if (string.IsNullOrWhiteSpace(counterparty.Name))
                throw new ArgumentException("Название контрагента не может быть пустым", nameof(counterparty));

            if (await ExistsAsync(counterparty.Name, ct).ConfigureAwait(false))
            {
                await using var checkContext = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                var existingWithSameName = await checkContext.Counterparties
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name == counterparty.Name, ct)
                    .ConfigureAwait(false);

                if (existingWithSameName != null && existingWithSameName.Id != counterparty.Id)
                    throw new InvalidOperationException($"Контрагент с названием «{counterparty.Name}» уже существует");
            }

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var existing = await context.Counterparties.FindAsync(new object[] { counterparty.Id }, ct).ConfigureAwait(false);
            if (existing == null)
                throw new InvalidOperationException($"Контрагент ID={counterparty.Id} не найден");

            existing.Name = counterparty.Name;
            existing.Type = counterparty.Type;
            existing.Phone = counterparty.Phone;
            existing.Notes = counterparty.Notes;
            existing.IsActive = counterparty.IsActive;

            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Обновлён контрагент ID={Id}, название={Name}", counterparty.Id, counterparty.Name);
        }

        /// <summary>
        /// Удалить контрагента. Удаление запрещено, если есть связанные операции (включая удалённые).
        /// </summary>
        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var counterparty = await context.Counterparties.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
            if (counterparty == null)
                throw new InvalidOperationException($"Контрагент ID={id} не найден");

            // Проверяем, есть ли связанные операции (включая удалённые — см. AGENTS.md 11.2)
            var hasTransactions = await context.Transactions
                .AnyAsync(t => t.CounterpartyId == id, ct)
                .ConfigureAwait(false);

            if (hasTransactions)
            {
                _log.Warning("Попытка удалить контрагента ID={Id} с привязанными операциями — отклонено", id);
                throw new InvalidOperationException("Нельзя удалить контрагента, у которого есть связанные операции");
            }

            context.Counterparties.Remove(counterparty);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.Information("Удалён контрагент ID={Id}", id);
        }

        /// <summary>
        /// Проверить существование контрагента с указанным именем (регистронезависимо)
        /// </summary>
        public async Task<bool> ExistsAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);

            // Быстрая проверка exact match на стороне БД
            var exactMatch = await context.Counterparties
                .AsNoTracking()
                .AnyAsync(c => c.Name == name, ct)
                .ConfigureAwait(false);

            if (exactMatch) return true;

            // Fallback: регистронезависимая проверка в памяти для Unicode/кириллицы
            var names = await context.Counterparties
                .AsNoTracking()
                .Select(c => c.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return names.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
