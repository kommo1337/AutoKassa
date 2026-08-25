using System.Globalization;
using System.Windows.Data;
using AutoKassa.Helpers.Converters;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Helpers
{
    public class MaxModalHeightConverterTests
    {
        private readonly MaxModalHeightConverter _converter = new();

        [Fact]
        public void Convert_SubtractsDefaultMargin()
        {
            _converter.Convert(1000d, typeof(double), null, CultureInfo.InvariantCulture)
                .Should().Be(952d);
        }

        [Fact]
        public void Convert_UsesParameterAsMargin()
        {
            _converter.Convert(1000d, typeof(double), "100", CultureInfo.InvariantCulture)
                .Should().Be(900d);
        }

        [Fact]
        public void Convert_ClampsToMinimum()
        {
            _converter.Convert(300d, typeof(double), null, CultureInfo.InvariantCulture)
                .Should().Be(320d);
            _converter.Convert(100d, typeof(double), null, CultureInfo.InvariantCulture)
                .Should().Be(320d);
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(-5d)]
        [InlineData(double.NaN)]
        public void Convert_InvalidHeight_ReturnsInfinity(double value)
        {
            _converter.Convert(value, typeof(double), null, CultureInfo.InvariantCulture)
                .Should().Be(double.PositiveInfinity);
        }

        [Fact]
        public void Convert_NonDouble_ReturnsInfinity()
        {
            _converter.Convert("строка", typeof(double), null, CultureInfo.InvariantCulture)
                .Should().Be(double.PositiveInfinity);
        }

        [Fact]
        public void ConvertBack_ReturnsBindingDoNothing()
        {
            _converter.ConvertBack(500d, typeof(double), null, CultureInfo.InvariantCulture)
                .Should().Be(Binding.DoNothing);
        }
    }
}
