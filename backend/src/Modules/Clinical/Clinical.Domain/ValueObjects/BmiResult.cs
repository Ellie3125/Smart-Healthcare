namespace Clinical.Domain.ValueObjects;

public enum BmiCategory
{
    Underweight,  // Gầy (< 18.5)
    Normal,       // Bình thường (18.5 - 22.9 theo chuẩn Châu Á WPRO)
    Overweight,   // Thừa cân (23.0 - 24.9)
    Obese         // Béo phì (>= 25.0)
}

public readonly record struct BmiResult(decimal Value, BmiCategory Category)
{
    public override string ToString() => $"{Value:F1} ({Category})";
}

public static class BmiCalculator
{
    /// <summary>
    /// Tính chỉ số khối cơ thể (BMI) từ cân nặng (kg) và chiều cao (cm) theo chuẩn Châu Á (WPRO).
    /// Công thức: BMI = Weight (kg) / (Height (m) ^ 2)
    /// </summary>
    public static BmiResult Calculate(decimal weightKg, decimal heightCm)
    {
        if (weightKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(weightKg), "Cân nặng phải lớn hơn 0 kg.");
        if (heightCm <= 0)
            throw new ArgumentOutOfRangeException(nameof(heightCm), "Chiều cao phải lớn hơn 0 cm.");

        var heightMeters = heightCm / 100m;
        var bmiValue = Math.Round(weightKg / (heightMeters * heightMeters), 1, MidpointRounding.AwayFromZero);

        var category = bmiValue switch
        {
            < 18.5m => BmiCategory.Underweight,
            <= 22.9m => BmiCategory.Normal,
            <= 24.9m => BmiCategory.Overweight,
            _ => BmiCategory.Obese,
        };

        return new BmiResult(bmiValue, category);
    }
}

