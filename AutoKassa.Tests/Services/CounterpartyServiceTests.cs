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
    public class CounterpartyServiceTests : IDisposable
    {
        private readonly AppDbContext _ctx;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _conn;
        private readonly CounterpartyService _svc;

        public CounterpartyServiceTests()
        {
            var (factory, conn) = TestDatabase.CreateWithFactory();
            _conn = conn;
            _ctx = factory.CreateDbContext();
            _svc = new CounterpartyService(factory);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }

        [Fact]
        public async Task AddAsync_ValidCounterparty_SetsCreatedAtAndIsActive()
        {
            var counterparty = new Counterparty
            {
                Name = "ООО Ромашка",
                Type = CounterpartyType.Supplier
            };

            var result = await _svc.AddAsync(counterparty);

            result.Id.Should().BeGreaterThan(0);
            result.IsActive.Should().BeTrue();
            result.CreatedAt.Should().NotBe(default);
        }

        [Fact]
        public async Task AddAsync_DuplicateName_ThrowsInvalidOperationException()
        {
            await _svc.AddAsync(new Counterparty { Name = "Дубликат", Type = CounterpartyType.Client });

            Func<Task> act = async () => await _svc.AddAsync(new Counterparty { Name = "дубликат", Type = CounterpartyType.Supplier });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task AddAsync_EmptyName_ThrowsArgumentException()
        {
            Func<Task> act = async () => await _svc.AddAsync(new Counterparty { Name = "   ", Type = CounterpartyType.Client });

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllCounterparties()
        {
            await _svc.AddAsync(new Counterparty { Name = "А", Type = CounterpartyType.Client });
            await _svc.AddAsync(new Counterparty { Name = "Б", Type = CounterpartyType.Supplier });

            var all = await _svc.GetAllAsync();

            all.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetActiveAsync_FiltersByType()
        {
            await _svc.AddAsync(new Counterparty { Name = "Клиент", Type = CounterpartyType.Client });
            await _svc.AddAsync(new Counterparty { Name = "Поставщик", Type = CounterpartyType.Supplier });

            var clients = await _svc.GetActiveAsync(CounterpartyType.Client);

            clients.Should().ContainSingle(c => c.Name == "Клиент");
        }

        [Fact]
        public async Task UpdateAsync_ChangesName()
        {
            var created = await _svc.AddAsync(new Counterparty { Name = "Старое", Type = CounterpartyType.Client });
            created.Name = "Новое";

            await _svc.UpdateAsync(created);
            var updated = await _svc.GetByIdAsync(created.Id);

            updated!.Name.Should().Be("Новое");
        }

        [Fact]
        public async Task UpdateAsync_DuplicateName_ThrowsInvalidOperationException()
        {
            var first = await _svc.AddAsync(new Counterparty { Name = "Первый", Type = CounterpartyType.Client });
            await _svc.AddAsync(new Counterparty { Name = "Второй", Type = CounterpartyType.Client });

            first.Name = "Второй";
            Func<Task> act = async () => await _svc.UpdateAsync(first);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task DeleteAsync_NoTransactions_RemovesCounterparty()
        {
            var created = await _svc.AddAsync(new Counterparty { Name = "Удаляемый", Type = CounterpartyType.Client });

            await _svc.DeleteAsync(created.Id);
            var deleted = await _svc.GetByIdAsync(created.Id);

            deleted.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_WithTransactions_ThrowsInvalidOperationException()
        {
            var counterparty = TestDatabase.SeedCounterparty(_ctx, "С операциями");
            var category = TestDatabase.SeedIncomeCategory(_ctx);
            TestDatabase.SeedTransaction(_ctx, category.Id, 100m, OperationType.Income, counterpartyId: counterparty.Id);

            Func<Task> act = async () => await _svc.DeleteAsync(counterparty.Id);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task ExistsAsync_ExistingName_ReturnsTrue()
        {
            await _svc.AddAsync(new Counterparty { Name = "Существующий", Type = CounterpartyType.Client });

            var exists = await _svc.ExistsAsync("Существующий");

            exists.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_NonExistingName_ReturnsFalse()
        {
            var exists = await _svc.ExistsAsync("Отсутствующий");

            exists.Should().BeFalse();
        }
    }
}
