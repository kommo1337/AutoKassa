using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Services
{
    /// <summary>
    /// Singleton-кэш справочников (категории, контрагенты, кредитные карты).
    /// Исключает повторные запросы к БД при каждом открытии редактора операции
    /// и при одновременной загрузке нескольких ViewModel.
    ///
    /// Инвалидация: сервисы справочников сбрасывают свою секцию после мутаций
    /// (Add/Update/Delete/Reorder), полный сброс — по <see cref="IDataChangeService.DataChanged"/>
    /// (охватывает восстановление бэкапа и прочие внешние изменения).
    ///
    /// Наружу отдаются клоны: мутация полученных объектов в ViewModel
    /// (например, CategoryItemViewModel.Model при переименовании) не портит кэш.
    /// </summary>
    public class ReferenceDataCache
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private List<Category> _categories;
        private List<Counterparty> _counterparties;
        private List<CreditCard> _creditCards;

        public ReferenceDataCache(
            IDbContextFactory<AppDbContext> contextFactory,
            IDataChangeService dataChangeService)
        {
            _contextFactory = contextFactory;
            // Singleton, отписка не нужна: живёт столько же, сколько IDataChangeService
            dataChangeService.DataChanged += InvalidateAll;
        }

        /// <summary>
        /// Полный сброс кэша (по событию DataChanged)
        /// </summary>
        public void InvalidateAll()
        {
            InvalidateCategories();
            InvalidateCounterparties();
            InvalidateCreditCards();
        }

        public void InvalidateCategories() => _categories = null;
        public void InvalidateCounterparties() => _counterparties = null;
        public void InvalidateCreditCards() => _creditCards = null;

        /// <summary>
        /// Все категории без сортировки (клоны); порядок задаёт вызывающий сервис
        /// </summary>
        public async Task<List<Category>> GetCategoriesAsync()
        {
            if (_categories != null)
                return _categories.Select(Clone).ToList();

            await _loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_categories == null)
                {
                    await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    _categories = await context.Categories
                        .AsNoTracking()
                        .ToListAsync()
                        .ConfigureAwait(false);
                }
                return _categories.Select(Clone).ToList();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Все контрагенты без сортировки (клоны); порядок задаёт вызывающий сервис
        /// </summary>
        public async Task<List<Counterparty>> GetCounterpartiesAsync()
        {
            if (_counterparties != null)
                return _counterparties.Select(Clone).ToList();

            await _loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_counterparties == null)
                {
                    await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    _counterparties = await context.Counterparties
                        .AsNoTracking()
                        .ToListAsync()
                        .ConfigureAwait(false);
                }
                return _counterparties.Select(Clone).ToList();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Все кредитные карты без сортировки (клоны); порядок задаёт вызывающий сервис
        /// </summary>
        public async Task<List<CreditCard>> GetCreditCardsAsync()
        {
            if (_creditCards != null)
                return _creditCards.Select(Clone).ToList();

            await _loadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_creditCards == null)
                {
                    await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
                    _creditCards = await context.CreditCards
                        .AsNoTracking()
                        .ToListAsync()
                        .ConfigureAwait(false);
                }
                return _creditCards.Select(Clone).ToList();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private static Category Clone(Category c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Type = c.Type,
            IsActive = c.IsActive,
            IsSystem = c.IsSystem,
            Color = c.Color,
            SortOrder = c.SortOrder,
            CreatedAt = c.CreatedAt
        };

        private static Counterparty Clone(Counterparty c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Type = c.Type,
            Phone = c.Phone,
            Notes = c.Notes,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt
        };

        private static CreditCard Clone(CreditCard c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            BankName = c.BankName,
            Limit = c.Limit,
            InterestRate = c.InterestRate,
            StatementDay = c.StatementDay,
            PaymentDay = c.PaymentDay,
            LastPaymentDate = c.LastPaymentDate,
            MinimumPaymentPercent = c.MinimumPaymentPercent,
            InitialDebt = c.InitialDebt,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt
        };
    }
}
