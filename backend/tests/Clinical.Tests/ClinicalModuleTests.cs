using Clinical.Application;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Xunit;

namespace Clinical.Tests;

public class ClinicalModuleTests
{
    private readonly FakeEncounterStore _encounterStore = new();
    private readonly FakeServiceOrderStore _serviceOrderStore = new();
    private readonly FakePrescriptionStore _prescriptionStore = new();
    private readonly FakeTimeProvider _clock = new(new DateTime(2026, 11, 5, 8, 30, 0, DateTimeKind.Utc));

    private ClinicalModule CreateModule() =>
        new(_encounterStore, _serviceOrderStore, _prescriptionStore, _clock);

    [Fact]
    public async Task StartEncounterAsync_creates_encounter_and_draft_clinical_record()
    {
        var module = CreateModule();
        var command = new StartEncounterCommand(
            AppointmentId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            OriginalDoctorId: Guid.NewGuid(),
            OriginalRoomId: Guid.NewGuid(),
            SpecialtySnapshot: "Nội tổng quát",
            DoctorRankSnapshot: "Bác sĩ Chuyên khoa II");

        var result = await module.StartEncounterAsync(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(command.AppointmentId, result.AppointmentId);
        Assert.Equal(command.PatientId, result.PatientId);
        Assert.Equal(command.OriginalDoctorId, result.OriginalDoctorId);
        Assert.Equal("InProgress", result.Status);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, result.CheckedInAtUtc);

        // Kiểm tra đã lưu Encounter vào store
        Assert.Single(_encounterStore.Encounters);
        var savedEncounter = _encounterStore.Encounters[0];
        Assert.Equal(result.Id, savedEncounter.Id);
        Assert.Equal(EncounterStatus.InProgress, savedEncounter.Status);

        // Kiểm tra đã tạo EncounterClinicalRecord rỗng ở trạng thái Draft
        Assert.Single(_encounterStore.Records);
        var savedRecord = _encounterStore.Records[0];
        Assert.Equal(result.Id, savedRecord.EncounterId);
        Assert.Equal(EncounterClinicalRecordStatus.Draft, savedRecord.Status);
        Assert.Equal(command.OriginalDoctorId, savedRecord.UpdatedByDoctorId);
    }

    [Fact]
    public async Task StartEncounterAsync_throws_when_patient_already_has_active_encounter()
    {
        var module = CreateModule();
        var patientId = Guid.NewGuid();

        // Giả lập bệnh nhân đang có một lượt khám InProgress
        _encounterStore.AddEncounter(new Encounter
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = patientId,
            OriginalDoctorId = Guid.NewGuid(),
            OriginalRoomId = Guid.NewGuid(),
            SpecialtySnapshot = "Nội",
            DoctorRankSnapshot = "BSCKI",
            CheckedInAtUtc = _clock.GetUtcNow().UtcDateTime.AddHours(-1),
            Status = EncounterStatus.InProgress
        });

        var command = new StartEncounterCommand(
            AppointmentId: Guid.NewGuid(),
            PatientId: patientId,
            OriginalDoctorId: Guid.NewGuid(),
            OriginalRoomId: Guid.NewGuid(),
            SpecialtySnapshot: "Tai Mũi Họng",
            DoctorRankSnapshot: "Thạc sĩ Bác sĩ");

        var ex = await Assert.ThrowsAsync<ClinicalException>(() =>
            module.StartEncounterAsync(command, CancellationToken.None));

        Assert.Equal(ClinicalErrorCode.ActiveEncounterAlreadyExists, ex.Code);
    }

    [Theory]
    [InlineData("", "BSCKI", "SpecialtySnapshot")]
    [InlineData("Nội", "", "DoctorRankSnapshot")]
    public async Task StartEncounterAsync_validates_required_snapshots(string specialty, string rank, string expectedField)
    {
        var module = CreateModule();
        var command = new StartEncounterCommand(
            AppointmentId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            OriginalDoctorId: Guid.NewGuid(),
            OriginalRoomId: Guid.NewGuid(),
            SpecialtySnapshot: specialty,
            DoctorRankSnapshot: rank);

        var ex = await Assert.ThrowsAsync<ClinicalException>(() =>
            module.StartEncounterAsync(command, CancellationToken.None));

        Assert.Equal(ClinicalErrorCode.InvalidRequest, ex.Code);
        Assert.Contains(expectedField, ex.Message);
    }

    [Fact]
    public async Task HasActiveEncounterAsync_returns_true_for_active_statuses()
    {
        var module = CreateModule();
        var patientId = Guid.NewGuid();

        Assert.False(await module.HasActiveEncounterAsync(patientId, CancellationToken.None));

        _encounterStore.AddEncounter(new Encounter
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = patientId,
            OriginalDoctorId = Guid.NewGuid(),
            OriginalRoomId = Guid.NewGuid(),
            SpecialtySnapshot = "Ngoại",
            DoctorRankSnapshot = "BS",
            CheckedInAtUtc = _clock.GetUtcNow().UtcDateTime,
            Status = EncounterStatus.WaitingCLS
        });

        Assert.True(await module.HasActiveEncounterAsync(patientId, CancellationToken.None));
    }

    [Fact]
    public async Task HasActiveEncounterAsync_returns_false_when_encounter_completed_or_abandoned()
    {
        var module = CreateModule();
        var patientId = Guid.NewGuid();

        _encounterStore.AddEncounter(new Encounter
        {
            Id = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            PatientId = patientId,
            OriginalDoctorId = Guid.NewGuid(),
            OriginalRoomId = Guid.NewGuid(),
            SpecialtySnapshot = "Ngoại",
            DoctorRankSnapshot = "BS",
            CheckedInAtUtc = _clock.GetUtcNow().UtcDateTime,
            Status = EncounterStatus.Completed,
            CompletedAtUtc = _clock.GetUtcNow().UtcDateTime
        });

        Assert.False(await module.HasActiveEncounterAsync(patientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetEncounterByIdAsync_returns_dto_when_exists()
    {
        var module = CreateModule();
        var encounterId = Guid.NewGuid();
        var encounter = new Encounter
        {
            Id = encounterId,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            OriginalDoctorId = Guid.NewGuid(),
            OriginalRoomId = Guid.NewGuid(),
            SpecialtySnapshot = "Nhi khoa",
            DoctorRankSnapshot = "BSCKII",
            CheckedInAtUtc = _clock.GetUtcNow().UtcDateTime,
            Status = EncounterStatus.InProgress
        };
        _encounterStore.AddEncounter(encounter);

        var result = await module.GetEncounterByIdAsync(encounterId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(encounterId, result.Id);
        Assert.Equal("Nhi khoa", result.SpecialtySnapshot);
        Assert.Equal("InProgress", result.Status);
    }
}

