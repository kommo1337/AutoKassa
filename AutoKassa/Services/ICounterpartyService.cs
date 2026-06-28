using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoKassa.Models;
using AutoKassa.Models.Enums;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы со справочником контрагентов
    /// </summary>
    public interface ICounterpartyService
    {
        /// <summary>
        /// Получить всех контрагентов
        /// </summary>
        Task<IReadOnlyList<Counterparty>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Получить активных контрагентов с возможностью фильтрации по типу
        /// </summary>
        Task<IReadOnlyList<Counterparty>> GetActiveAsync(CounterpartyType? type = null, CancellationToken ct = default);

        /// <summary>
        /// Получить контрагента по ID
        /// </summary>
        Task<Counterparty?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Добавить нового контрагента
        /// </summary>
        Task<Counterparty> AddAsync(Counterparty counterparty, CancellationToken ct = default);

        /// <summary>
        /// Обновить контрагента
        /// </summary>
        Task UpdateAsync(Counterparty counterparty, CancellationToken ct = default);

        /// <summary>
        /// Удалить контрагента. Удаление запрещено, если есть связанные операции.
        /// </summary>
        Task DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Проверить существование контрагента с указанным именем
        /// </summary>
        Task<bool> ExistsAsync(string name, CancellationToken ct = default);
    }
}
