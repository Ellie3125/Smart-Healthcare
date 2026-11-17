using Xunit;

namespace SqlServerTests;

public sealed class TestDatabaseGuardTests
{
    [Theory]
    [InlineData("SmartHealthcare_Test")]
    [InlineData("SmartHealthcare_Test_Codex")]
    public void Accepts_only_the_two_authorized_test_databases(string name) => TestDatabase.EnsureTestDatabase(name);

    [Theory]
    [InlineData("SmartHealthcare")]
    [InlineData("master")]
    [InlineData("SmartHealthcare_Test_Prod")]
    [InlineData("SmartHealthcare_Test_Codex_Extra")]
    [InlineData("")]
    public void Rejects_non_test_databases(string name) =>
        Assert.Throws<InvalidOperationException>(() => TestDatabase.EnsureTestDatabase(name));
}
