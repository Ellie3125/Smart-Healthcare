namespace Clinical.Domain.ValueObjects;

public readonly record struct BloodPressure
{
    public decimal Systolic { get; }
    public decimal Diastolic { get; }

    public BloodPressure(decimal systolic, decimal diastolic)
    {
        if (systolic <= 0)
            throw new ArgumentOutOfRangeException(nameof(systolic), "Huyết áp tâm thu phải lớn hơn 0 mmHg.");
        if (diastolic <= 0)
            throw new ArgumentOutOfRangeException(nameof(diastolic), "Huyết áp tâm trương phải lớn hơn 0 mmHg.");
        if (systolic <= diastolic)
            throw new ArgumentException("Huyết áp tâm thu (Systolic) phải lớn hơn huyết áp tâm trương (Diastolic).");

        Systolic = systolic;
        Diastolic = diastolic;
    }

    /// <summary>Phát hiện cơn tăng huyết áp khẩn cấp (Hypertensive Crisis: Tâm thu > 180 hoặc Tâm trương > 120).</summary>
    public bool IsCrisis => Systolic > 180 || Diastolic > 120;

    /// <summary>Phát hiện tụt huyết áp (Hypotension: Tâm thu < 90 hoặc Tâm trương < 60).</summary>
    public bool IsHypotension => Systolic < 90 || Diastolic < 60;

    public override string ToString() => $"{Systolic:F0}/{Diastolic:F0} mmHg";
}

