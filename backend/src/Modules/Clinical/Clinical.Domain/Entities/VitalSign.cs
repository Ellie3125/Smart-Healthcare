namespace Clinical.Domain.Entities;

public sealed class VitalSign
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? SystolicBP { get; set; }
    public decimal? DiastolicBP { get; set; }
    public int? PulseRate { get; set; }
    public decimal? BodyTemperature { get; set; }
    public decimal? SpO2 { get; set; }
    // Nhịp thở (lần/phút); ghi chú của điều dưỡng cho lần đo này. Một lần đo một dòng, không ghi đè.
    public int? RespiratoryRate { get; set; }
    public string? NurseNote { get; set; }
    // ID chéo module: identity.Staff, không FK
    public Guid RecordedByStaffId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
