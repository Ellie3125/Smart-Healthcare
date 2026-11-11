using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Application.DoctorSearch;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace SmartHealthcare.Api;

/// <summary>
/// Dữ liệu demo để đặt thử trên máy dev: bảng giá phí khám theo cấp bậc và lịch khám các ngày tới
/// cho bác sĩ phòng khám đã seed ở IdentityPatients. Chỉ chạy ở Development khi bật <c>Seed:DemoData</c>
/// (launchSettings.json đã bật sẵn); test và Staging/Prod không chạy.
/// </summary>
public static class DemoData
{
    public const string Flag = "Seed:DemoData";
    public const string PriceListCode = "DEMO-PHI-KHAM";
    public const int DaysAhead = 7;

    // Identity chưa lưu phòng cố định của bác sĩ (chỉ có ca trực theo ngày) nên phòng khám demo khai ở đây.
    public static readonly (Guid DoctorId, Guid RoomId)[] DoctorRooms =
    [
        (SeedConstants.Doctor02Id, SeedConstants.RoomPK01Id),
        (SeedConstants.Doctor03Id, SeedConstants.RoomPK02Id),
        (SeedConstants.Doctor04Id, SeedConstants.RoomPK03Id),
        (SeedConstants.Doctor07Id, SeedConstants.RoomPK04Id),
        (SeedConstants.Doctor08Id, SeedConstants.RoomPK05Id),
        (SeedConstants.Doctor09Id, SeedConstants.RoomPK06Id),
        (SeedConstants.Doctor10Id, SeedConstants.RoomPK10Id),
    ];

    // Giá demo, không phải bảng giá đã duyệt.
    private static readonly (Guid RankId, decimal Fee)[] ExamFees =
    [
        (SeedConstants.RankDoctorId, 150_000m),
        (SeedConstants.RankSpecialistIId, 200_000m),
        (SeedConstants.RankMasterId, 200_000m),
        (SeedConstants.RankSpecialistIIId, 250_000m),
        (SeedConstants.RankAssociateProfId, 350_000m),
        (SeedConstants.RankProfessorId, 500_000m),
    ];

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() && configuration.GetValue<bool>(Flag);

    /// <summary>Cho Scheduling thấy bác sĩ thật của Identity thay cho danh sách khai tay trong cấu hình.</summary>
    public static void AddDemoData(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(DoctorRooms.Select(x => KeyValuePair.Create(
            $"Scheduling:DoctorDays:DevelopmentDoctorRooms:{x.DoctorId:D}", (string?)x.RoomId.ToString("D"))));
        builder.Services.AddScoped<IDoctorSearchDirectory, IdentityDoctorSearchDirectory>();
    }

    public static async Task SeedDemoDataAsync(this WebApplication app, CancellationToken ct = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await SeedAsync(services.GetRequiredService<BillingDbContext>(), services.GetRequiredService<SchedulingDbContext>(),
            services.GetRequiredService<DoctorDayService>(), DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), ct);
    }

    /// <summary>Chạy lại nhiều lần không sinh trùng; ngày đã có (kể cả ngày admin đã đóng) được giữ nguyên.</summary>
    public static async Task SeedAsync(BillingDbContext billing, SchedulingDbContext scheduling, DoctorDayService days,
        DateOnly today, CancellationToken ct)
    {
        if (!await billing.Set<PriceList>().AnyAsync(x => x.PriceListCode == PriceListCode, ct))
        {
            var priceList = new PriceList
            {
                Id = Guid.NewGuid(),
                PriceListCode = PriceListCode,
                PriceListName = "Bảng giá phí khám demo",
                Currency = "VND",
                EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };
            billing.Add(priceList);
            billing.AddRange(ExamFees.Select(x => new PriceListItem
            {
                Id = Guid.NewGuid(),
                PriceListId = priceList.Id,
                ItemType = ItemType.ConsultationFee,
                DoctorRankId = x.RankId,
                UnitPrice = x.Fee,
                IsActive = true
            }));
            await billing.SaveChangesAsync(ct);
        }

        var lastDate = today.AddDays(DaysAhead - 1);
        var doctorIds = DoctorRooms.Select(x => x.DoctorId).ToArray();
        var existing = (await scheduling.Set<DoctorDayQuota>().AsNoTracking()
                .Where(x => doctorIds.Contains(x.DoctorId) && x.ServiceDate >= today && x.ServiceDate <= lastDate)
                .Select(x => new { x.DoctorId, x.ServiceDate })
                .ToListAsync(ct))
            .Select(x => (x.DoctorId, x.ServiceDate))
            .ToHashSet();

        foreach (var doctorId in doctorIds)
        {
            for (var date = today; date <= lastDate; date = date.AddDays(1))
            {
                // OpenAsync mở lại ngày đã đóng, nên chỉ gọi cho ngày chưa có.
                if (!existing.Contains((doctorId, date)))
                {
                    await days.OpenAsync(doctorId, date, ct);
                }
            }
        }
    }
}

internal sealed class IdentityDoctorSearchDirectory(IIdentityPatientsModule identity) : IDoctorSearchDirectory
{
    public async Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct) =>
        (await identity.SearchDoctorsAsync(new DoctorSearch(IsActive: true), ct))
        .Select(x => new DoctorSearchProfile(x.DoctorId, x.FullName, x.RankId, x.SpecialtyId))
        .ToList();
}
