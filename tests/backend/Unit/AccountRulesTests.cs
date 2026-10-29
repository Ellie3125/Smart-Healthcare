using SmartHealthcare.Identity.Domain;

namespace SmartHealthcare.UnitTests;

public class AccountRulesTests
{
    [Theory]
    [InlineData("+84 901 234 567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("090.123.4567", "0901234567")]
    public void Phone_numbers_are_normalized_to_local_format(string input, string expected)
    {
        var normalized = AccountIdentifiers.NormalizePhone(input);

        Assert.Equal(expected, normalized);
        Assert.True(AccountIdentifiers.IsValidPhone(normalized));
    }

    [Fact]
    public void Email_is_trimmed_and_lowercased() =>
        Assert.Equal("an.nguyen@gmail.com", AccountIdentifiers.NormalizeEmail("  An.Nguyen@Gmail.com "));

    [Theory]
    [InlineData("ab", false)]
    [InlineData("an.nguyen_90", true)]
    [InlineData("an nguyen", false)]
    [InlineData("nguyễn", false)]
    public void User_name_rules(string userName, bool valid) =>
        Assert.Equal(valid, AccountIdentifiers.IsValidUserName(userName));

    [Theory]
    [InlineData("short1", false)]
    [InlineData("onlyletters", false)]
    [InlineData("12345678", false)]
    [InlineData("matkhau123", true)]
    public void Password_policy(string password, bool valid) =>
        Assert.Equal(valid, PasswordPolicy.Check(password) is null);
}
