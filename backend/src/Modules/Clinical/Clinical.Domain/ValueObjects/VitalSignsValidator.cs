namespace Clinical.Domain.ValueObjects;

public static class VitalSignsValidator
{
    public static void Validate(
        decimal? weightKg,
        decimal? heightCm,
        decimal? systolicBp,
        decimal? diastolicBp,
        int? pulseRate,
        decimal? bodyTemperature,
        decimal? spO2)
    {
        if (weightKg.HasValue && weightKg.Value is < 0.5m or > 500m)
            throw new ArgumentOutOfRangeException(nameof(weightKg), "Cân nặng phải trong khoảng từ 0.5 kg đến 500 kg.");

        if (heightCm.HasValue && heightCm.Value is < 20m or > 260m)
            throw new ArgumentOutOfRangeException(nameof(heightCm), "Chiều cao phải trong khoảng từ 20 cm đến 260 cm.");

        if (bodyTemperature.HasValue && bodyTemperature.Value is < 30.0m or > 45.0m)
            throw new ArgumentOutOfRangeException(nameof(bodyTemperature), "Nhiệt độ cơ thể phải trong khoảng từ 30.0°C đến 45.0°C.");

        if (pulseRate.HasValue && pulseRate.Value is < 20 or > 250)
            throw new ArgumentOutOfRangeException(nameof(pulseRate), "Nhịp tim phải trong khoảng từ 20 đến 250 nhịp/phút.");

        if (spO2.HasValue && spO2.Value is < 40m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(spO2), "Nồng độ SpO2 phải trong khoảng từ 40% đến 100%.");

        if (systolicBp.HasValue && systolicBp.Value is < 40m or > 300m)
            throw new ArgumentOutOfRangeException(nameof(systolicBp), "Huyết áp tâm thu phải trong khoảng từ 40 mmHg đến 300 mmHg.");

        if (diastolicBp.HasValue && diastolicBp.Value is < 20m or > 200m)
            throw new ArgumentOutOfRangeException(nameof(diastolicBp), "Huyết áp tâm trương phải trong khoảng từ 20 mmHg đến 200 mmHg.");

        if (systolicBp.HasValue && diastolicBp.HasValue && systolicBp.Value <= diastolicBp.Value)
            throw new ArgumentException("Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.", nameof(systolicBp));
    }
}
