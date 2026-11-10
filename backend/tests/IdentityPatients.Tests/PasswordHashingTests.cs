using IdentityPatients.Infrastructure.Security;
using Xunit;

namespace IdentityPatients.Tests;

public sealed class PasswordHashingTests
{
    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue() =>
        Assert.True(PasswordHashing.Verify("mat-khau-dung", PasswordHashing.Hash("mat-khau-dung")));

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse() =>
        Assert.False(PasswordHashing.Verify("mat-khau-sai", PasswordHashing.Hash("mat-khau-dung")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-phai-hash")]
    [InlineData("600000.abc")]
    [InlineData("abc.AAAA.AAAA")]
    [InlineData("0.AAAAAAAAAAAAAAAAAAAAAA==.AAAA")]
    [InlineData("600000.!!!.@@@")]
    [InlineData("600000..")]
    public void Verify_MalformedHash_ReturnsFalseWithoutThrowing(string? stored) =>
        Assert.False(PasswordHashing.Verify("bat-ky", stored));

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentStrings()
    {
        var first = PasswordHashing.Hash("cung-mat-khau");
        var second = PasswordHashing.Hash("cung-mat-khau");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHashing.Verify("cung-mat-khau", first));
        Assert.True(PasswordHashing.Verify("cung-mat-khau", second));
    }

    [Fact]
    public void Hash_UsesFormatIterationsSaltKey()
    {
        var parts = PasswordHashing.Hash("x").Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal("600000", parts[0]);
        Assert.Equal(16, Convert.FromBase64String(parts[1]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[2]).Length);
    }
}
