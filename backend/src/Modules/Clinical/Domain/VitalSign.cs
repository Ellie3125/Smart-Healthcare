namespace SmartHealthcare.Clinical.Domain;

public sealed class VitalSign
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public decimal SystolicBP { get; set; }
    public decimal DiastolicBP { get; set; }
    public int PulseRate { get; set; }
    public decimal BodyTemperature { get; set; }
    public decimal SpO2 { get; set; }
    public Guid RecordedByStaffId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
