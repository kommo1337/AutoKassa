using AutoKassa.Models.Enums;
using AutoKassa.Services.Dashboard;
using FluentAssertions;
using System;
using Xunit;

namespace AutoKassa.Tests.Services.Dashboard
{
    /// <summary>
    /// Тесты координатора периода дашборда.
    /// </summary>
    public class DashboardPeriodCoordinatorTests
    {
        private readonly DashboardPeriodCoordinator _coordinator = new();

        [Fact]
        public void SetPeriod_Month_ReturnsFirstDayOfMonthToToday()
        {
            var today = DateTime.Today;
            var expectedFrom = new DateTime(today.Year, today.Month, 1);

            var period = _coordinator.SetPeriod(PeriodType.Month);

            period.From.Should().Be(expectedFrom);
            period.To.Should().Be(today);
            period.Label.Should().Be("ЗА МЕСЯЦ");
            period.ButtonLabel.Should().Be("Месяц");
        }

        [Fact]
        public void SetPeriod_AllTime_Returns2000ToToday()
        {
            var today = DateTime.Today;

            var period = _coordinator.SetPeriod(PeriodType.AllTime);

            period.From.Should().Be(new DateTime(2000, 1, 1));
            period.To.Should().Be(today);
            period.Label.Should().Be("ЗА ВСЁ ВРЕМЯ");
            period.ButtonLabel.Should().Be("Всё время");
        }

        [Fact]
        public void SetCustomPeriod_ReturnsPassedDatesAndCustomLabels()
        {
            var from = new DateTime(2025, 1, 15);
            var to = new DateTime(2025, 2, 20);

            var period = _coordinator.SetCustomPeriod(from, to);

            period.From.Should().Be(from.Date);
            period.To.Should().Be(to.Date);
            period.Label.Should().Be("ЗА ПЕРИОД");
            period.ButtonLabel.Should().Be("Период");
        }
    }
}
