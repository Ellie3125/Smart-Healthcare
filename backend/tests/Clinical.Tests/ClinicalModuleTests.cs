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

    [Theory]
    [InlineData(EncounterClinicalRecordStatus.Draft, 0)]
    [InlineData(EncounterClinicalRecordStatus.Published, 1)]
    public async Task Published_diagnosis_reader_filters_draft_and_missing_records(EncounterClinicalRecordStatus status, int count)
    {
        var id = AddEncounter(Guid.NewGuid());
        _encounterStore.Records.Add(new() { EncounterId = id, Status = status });
        _encounterStore.Diagnoses.Add(new() { EncounterId = id, DiagnosisName = "Test", ICD10Code = "K29.7", DiagnosisType = DiagnosisType.Final });
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(count, (await CreateModule().GetPublishedDiagnosesAsync(id, ct)).Count);
        Assert.Empty(await CreateModule().GetPublishedDiagnosesAsync(Guid.NewGuid(), ct));
        var error = await Assert.ThrowsAsync<ClinicalException>(() => CreateModule().GetPublishedDiagnosesAsync(Guid.Empty, ct));
        Assert.Equal(ClinicalErrorCode.InvalidRequest, error.Code);
    }

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

    [Fact]
    public async Task IsOriginalDoctorAsync_is_true_only_for_the_original_doctor()
    {
        var doctorId = Guid.NewGuid();
        var encounterId = AddEncounter(doctorId);
        var module = CreateModule();

        Assert.True(await module.IsOriginalDoctorAsync(doctorId, encounterId, CancellationToken.None));
        Assert.False(await module.IsOriginalDoctorAsync(Guid.NewGuid(), encounterId, CancellationToken.None));
        Assert.False(await module.IsOriginalDoctorAsync(doctorId, Guid.NewGuid(), CancellationToken.None));
        Assert.False(await module.IsOriginalDoctorAsync(Guid.Empty, encounterId, CancellationToken.None));
    }

    [Theory]
    [InlineData(EncounterClinicalRecordStatus.Published, "Kết luận sau CLS", true)]
    [InlineData(EncounterClinicalRecordStatus.Published, null, false)]
    [InlineData(EncounterClinicalRecordStatus.Published, "  ", false)]
    [InlineData(EncounterClinicalRecordStatus.Draft, "Kết luận sau CLS", false)]
    public async Task IsPostClsConclusionPublishedAsync_requires_published_record_with_conclusion(
        EncounterClinicalRecordStatus status, string? conclusion, bool expected)
    {
        var encounterId = AddEncounter(Guid.NewGuid());
        _encounterStore.AddClinicalRecord(new EncounterClinicalRecord
        {
            Id = Guid.NewGuid(),
            EncounterId = encounterId,
            Status = status,
            PostCLSConclusion = conclusion,
        });

        Assert.Equal(expected, await CreateModule().IsPostClsConclusionPublishedAsync(encounterId, CancellationToken.None));
    }

    [Fact]
    public async Task IsPostClsConclusionPublishedAsync_is_false_without_record() =>
        Assert.False(await CreateModule().IsPostClsConclusionPublishedAsync(Guid.NewGuid(), CancellationToken.None));

    private Guid AddEncounter(Guid doctorId)
    {
        var id = Guid.NewGuid();
        _encounterStore.AddEncounter(new Encounter
        {
            Id = id,
            AppointmentId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            OriginalDoctorId = doctorId,
            OriginalRoomId = Guid.NewGuid(),
            SpecialtySnapshot = "Nội",
            DoctorRankSnapshot = "BS",
            Status = EncounterStatus.WaitingCLS,
        });
        return id;
    }
}

