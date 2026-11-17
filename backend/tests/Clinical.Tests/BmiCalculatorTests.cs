using Clinical.Domain.ValueObjects;
using Xunit;

namespace Clinical.Tests;

public class BmiCalculatorTests
{
    [Theory]
    [InlineData(45, 165, 16.5, BmiCategory.Underweight)]
    [InlineData(55, 165, 20.2, BmiCategory.Normal)]
    [InlineData(65, 165, 23.9, BmiCategory.Overweight)]
    [InlineData(75, 165, 27.5, BmiCategory.Obese)]
    public void Calculate_returns_expected_bmi_and_category(decimal weight, decimal height, decimal expectedValue, BmiCategory expectedCategory)
    {
        var result = BmiCalculator.Calculate(weight, height);

        Assert.Equal(expectedValue, result.Value);
        Assert.Equal(expectedCategory, result.Category);
    }

    [Theory]
    [InlineData(0, 170)]
    [InlineData(-5, 170)]
    [InlineData(60, 0)]
    [InlineData(60, -10)]
    public void Calculate_throws_on_invalid_dimensions(decimal weight, decimal height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BmiCalculator.Calculate(weight, height));
    }
}

