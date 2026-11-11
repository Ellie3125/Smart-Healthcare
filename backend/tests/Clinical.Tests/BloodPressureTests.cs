using Clinical.Domain.ValueObjects;
using Xunit;

namespace Clinical.Tests;

public class BloodPressureTests
{
    [Fact]
    public void Valid_blood_pressure_initializes_and_formats_correctly()
    {
        var bp = new BloodPressure(120, 80);

        Assert.Equal(120, bp.Systolic);
        Assert.Equal(80, bp.Diastolic);
        Assert.False(bp.IsCrisis);
        Assert.False(bp.IsHypotension);
        Assert.Equal("120/80 mmHg", bp.ToString());
    }

    [Fact]
    public void Systolic_less_than_or_equal_to_diastolic_throws_argument_exception()
    {
        var ex = Assert.Throws<ArgumentException>(() => new BloodPressure(80, 120));
        Assert.Contains("Systolic", ex.Message);

        Assert.Throws<ArgumentException>(() => new BloodPressure(90, 90));
    }

    [Theory]
    [InlineData(190, 100)]
    [InlineData(140, 125)]
    [InlineData(200, 130)]
    public void Detects_hypertensive_crisis(decimal systolic, decimal diastolic)
    {
        var bp = new BloodPressure(systolic, diastolic);
        Assert.True(bp.IsCrisis);
    }

    [Theory]
    [InlineData(85, 55)]
    [InlineData(100, 50)]
    public void Detects_hypotension(decimal systolic, decimal diastolic)
    {
        var bp = new BloodPressure(systolic, diastolic);
        Assert.True(bp.IsHypotension);
    }
}

