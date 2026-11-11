using Shared.Infrastructure;
using Xunit;

namespace Shared.Infrastructure.Tests;

public sealed class DatabaseSourceTests
{
    [Theory]
    [InlineData(null, "SmartHealthcare")]
    [InlineData("", "SmartHealthcare")]
    [InlineData("Local", "SmartHealthcare")]
    [InlineData("Docker", "SmartHealthcareDocker")]
    [InlineData("docker", "SmartHealthcareDocker")]
    public void ConnectionName_follows_database_source(string? source, string expected) =>
        Assert.Equal(expected, DatabaseSource.ConnectionName(source));
}
