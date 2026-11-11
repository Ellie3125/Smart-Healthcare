using Clinical.Domain.ValueObjects;
using Xunit;

namespace Clinical.Tests;

public class VitalSignsValidatorTests
{
    [Fact]
    public void Valid_vital_signs_pass_validation()
    {
        // Không ném ngoại lệ
        VitalSignsValidator.Validate(
            weightKg: 65.5m,
            heightCm: 170.0m,
            systolicBp: 120m,
            diastolicBp: 80m,
            pulseRate: 75,
            bodyTemperature: 36.8m,
            spO2: 98.0m);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(550.0)]
    public void Out_of_range_weight_throws(decimal weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VitalSignsValidator.Validate(weight, 170m, 120m, 80m, 75, 37m, 98m));
    }

    [Theory]
    [InlineData(15.0)]
    [InlineData(300.0)]
    public void Out_of_range_height_throws(decimal height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VitalSignsValidator.Validate(60m, height, 120m, 80m, 75, 37m, 98m));
    }

    [Theory]
    [InlineData(28.0)]
    [InlineData(46.0)]
    public void Out_of_range_temperature_throws(decimal temp)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VitalSignsValidator.Validate(60m, 170m, 120m, 80m, 75, temp, 98m));
    }

    [Fact]
    public void Systolic_not_higher_than_diastolic_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            VitalSignsValidator.Validate(60m, 170m, 80m, 120m, 75, 37m, 98m));
    }
}

