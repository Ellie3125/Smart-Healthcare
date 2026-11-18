using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

// Ràng buộc DB của hai bảng lịch sử: bản 1 là Published, bản sau phải có lý do, số bản không trùng, JSON hợp lệ.
// Mọi thứ chạy trong một transaction rồi rollback, không để lại dữ liệu.
[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClinicalRevisionSchemaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Clinical_record_revisions_enforce_order_reason_uniqueness_and_json()
    {
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var recordId = await SeedRecordAsync(db);
        var doctor = Guid.NewGuid();

        async Task Insert(int number, string kind, string? reason, string json = "[]") =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT clinical.EncounterClinicalRecordRevision (Id, ClinicalRecordId, RevisionNumber, Kind, PostCLSConclusion, DiagnosesJson, Reason, ChangedByDoctorId, ChangedAtUtc)
                VALUES ({Guid.NewGuid()}, {recordId}, {number}, {kind}, N'kết luận', {json}, {reason}, {doctor}, SYSUTCDATETIME())
                """, Ct);

        await AssertSqlError(547, () => Insert(2, "Published", null));          // Published chỉ là bản 1
        await AssertSqlError(547, () => Insert(1, "Published", null, "not json"));
        await AssertSqlError(547, () => Insert(1, "Amended", "lý do"));         // đính chính không thể là bản 1
        await Insert(1, "Published", null, """[{"icd10Code":"J06.9","diagnosisName":"Viêm họng","isPrimary":true}]""");
        await AssertSqlError(547, () => Insert(2, "Amended", null));            // đính chính thiếu lý do
        await AssertSqlError(547, () => Insert(2, "Amended", "   "));
        await Insert(2, "Amended", "Bổ sung kết luận sau CLS");
        await AssertSqlError(2601, () => Insert(2, "Amended", "trùng số bản"));

        Assert.Equal(2, await db.Set<EncounterClinicalRecordRevision>().CountAsync(x => x.ClinicalRecordId == recordId, Ct));
        await tx.RollbackAsync(Ct);
    }

    [Fact]
    public async Task Cls_result_revisions_require_reason_after_first_publish()
    {
        await using var services = Services();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(Ct);
        var resultId = await SeedClsResultAsync(db);
        var account = Guid.NewGuid();

        async Task Insert(int number, string kind, string? reason) =>
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT clinical.CLSResultRevision (Id, CLSResultId, RevisionNumber, Kind, ResultSummary, DetailedFindings, Reason, ChangedByAccountId, ChangedAtUtc)
                VALUES ({Guid.NewGuid()}, {resultId}, {number}, {kind}, N'bình thường', N'chi tiết', {reason}, {account}, SYSUTCDATETIME())
                """, Ct);

        await AssertSqlError(547, () => Insert(0, "Published", null));
        await Insert(1, "Published", null);
        await AssertSqlError(547, () => Insert(2, "Corrected", null));
        await AssertSqlError(547, () => Insert(2, "Withdrawn", ""));
        await Insert(2, "Corrected", "Nhập nhầm chỉ số");
        await Insert(3, "Withdrawn", "Trả nhầm bệnh nhân");
        await AssertSqlError(2601, () => Insert(3, "Corrected", "trùng số bản"));

        Assert.Equal(3, await db.Set<CLSResultRevision>().CountAsync(x => x.CLSResultId == resultId, Ct));
        await tx.RollbackAsync(Ct);
    }

    private static ServiceProvider Services()
    {
        var cs = TestDatabase.RequiredConnectionString();
        return new ServiceCollection().AddSharedInfrastructure(cs).AddClinicalInfrastructure(cs).BuildServiceProvider();
    }

    private static async Task AssertSqlError(int number, Func<Task> act) =>
        Assert.Equal(number, (await Assert.ThrowsAsync<SqlException>(act)).Number);

    private static async Task<Guid> SeedEncounterAsync(ClinicalDbContext db)
    {
        var encounter = new Encounter { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            OriginalDoctorId = Guid.NewGuid(), OriginalRoomId = Guid.NewGuid(), SpecialtySnapshot = "Test",
            DoctorRankSnapshot = "Test", Status = EncounterStatus.InProgress, CheckedInAtUtc = DateTime.UtcNow };
        db.Add(encounter);
        await db.SaveChangesAsync(Ct);
        return encounter.Id;
    }

    private static async Task<Guid> SeedRecordAsync(ClinicalDbContext db)
    {
        var record = new EncounterClinicalRecord { Id = Guid.NewGuid(), EncounterId = await SeedEncounterAsync(db),
            Status = EncounterClinicalRecordStatus.Published, RecordedAtUtc = DateTime.UtcNow, PublishedAtUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow, UpdatedByDoctorId = Guid.NewGuid() };
        db.Add(record);
        await db.SaveChangesAsync(Ct);
        return record.Id;
    }

    private static async Task<Guid> SeedClsResultAsync(ClinicalDbContext db)
    {
        var order = new ServiceOrder { Id = Guid.NewGuid(), EncounterId = await SeedEncounterAsync(db), OrderRoundNumber = 1,
            OrderedAtUtc = DateTime.UtcNow, PrescribedByDoctorId = Guid.NewGuid(), OrderBatchStatus = OrderBatchStatus.Confirmed };
        var item = new ServiceOrderItem { Id = Guid.NewGuid(), ServiceOrderId = order.Id, MedicalServiceId = Guid.NewGuid(),
            ServiceNameSnapshot = "Xét nghiệm", AppliedPrice = 90000, ItemStatus = ItemStatus.ServiceFinished, IsMandatory = true };
        var result = new CLSResult { Id = Guid.NewGuid(), ServiceOrderItemId = item.Id, ResultSummary = "bình thường",
            DetailedFindings = "chi tiết", ResultStatus = ResultStatus.Published, PublishedAtUtc = DateTime.UtcNow };
        db.AddRange(order, item, result);
        await db.SaveChangesAsync(Ct);
        return result.Id;
    }
}
