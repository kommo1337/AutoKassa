using AutoKassa.Services;
using FluentAssertions;
using Xunit;

namespace AutoKassa.Tests.Services
{
    public class PasswordServiceTests
    {
        private readonly PasswordService _svc = new();

        [Fact]
        public void HashPassword_ReturnsNonNullNonEmptyString()
        {
            var hash = _svc.HashPassword("test123");

            hash.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void HashPassword_SamePasswordProducesDifferentHashes()
        {
            var hash1 = _svc.HashPassword("password");
            var hash2 = _svc.HashPassword("password");

            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            var hash = _svc.HashPassword("myPassword");

            _svc.VerifyPassword("myPassword", hash).Should().BeTrue();
        }

        [Fact]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("correctPassword");

            _svc.VerifyPassword("wrongPassword", hash).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_NullPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("test");

            _svc.VerifyPassword(null!, hash).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_EmptyPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("test");

            _svc.VerifyPassword(string.Empty, hash).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_NullHash_ReturnsFalse()
        {
            _svc.VerifyPassword("test", null!).Should().BeFalse();
        }

        [Fact]
        public void VerifyPassword_InvalidHashString_ReturnsFalse()
        {
            _svc.VerifyPassword("test", "not-a-valid-bcrypt-hash").Should().BeFalse();
        }
    }
}
