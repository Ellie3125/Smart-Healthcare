using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Infrastructure.Seed;
using Shared.Abstractions;

namespace SmartHealthcare.Api;

/// <summary>
/// Cách seed duy nhất của hệ thống: <c>dotnet SmartHealthcare.Api.dll seed</c> (máy dev: <c>dotnet run -- seed</c>;
/// Compose: <c>docker compose run --rm api seed</c>). Chạy một lần sau migration rồi thoát, Api không tự seed khi khởi động.
/// Tạo dữ liệu GIẢ LẬP cho dev/Staging: tài khoản, chuyên khoa, cấp bậc, phòng (Identity), dịch vụ CLS + phòng thực hiện
/// + vị trí phục vụ (Scheduling), bảng giá phí khám và CLS (Billing), lịch trực 7 ngày tới cho bác sĩ demo.
/// Chạy lại nhiều lần chỉ thêm phần còn thiếu, không mở lại ngày admin đã đóng. Từ chối chạy ở Production.
/// </summary>
public static class SeedCommand
{
    public const string Name = "seed";
    public const string PriceListCode = "DEV-2026";
    private static readonly Guid PriceListId = Guid.Parse("0a1f0004-0000-4000-8000-000000000001");
    public const int DaysAhead = 7;

    // Phòng khám cố định của bác sĩ demo khi mở lịch.
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

    // Giá giả lập, không phải bảng giá đã duyệt.
    private static readonly (Guid RankId, decimal Fee)[] ExamFees =
    [
        (SeedConstants.RankSpecialistIId, 150_000m),
        (SeedConstants.RankSpecialistIIId, 200_000m),
        (SeedConstants.RankMasterId, 180_000m),
        (SeedConstants.RankDoctorId, 250_000m),
        (SeedConstants.RankAssociateProfId, 350_000m),
        (SeedConstants.RankProfessorId, 500_000m),
    ];

    // Mã dịch vụ do SchedulingSeed tạo.
    private static readonly (string ServiceCode, decimal Fee)[] ClsFees =
    [
        ("DEV-XN-CTM", 90_000m),
        ("DEV-XN-SH", 150_000m),
        ("DEV-XQ-NGUC", 120_000m),
        ("DEV-SA-BUNG", 200_000m),
        ("DEV-NS-DD", 800_000m),
    ];

    public static bool IsRequested(string[] args) => args.Length == 1 && args[0] == Name;

    public static async Task RunAsync(WebApplication app, CancellationToken ct = default)
    {
        if (app.Environment.IsProduction())
            throw new InvalidOperationException("Không seed dữ liệu giả lập ở Production.");

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IdentityPatientsSeedService>().SeedAsync(ct);

        var billing = services.GetRequiredService<BillingDbContext>();
        var scheduling = services.GetRequiredService<SchedulingDbContext>();
        var rooms = await services.GetRequiredService<IIdentityPatientsModule>().GetRoomsAsync(ct: ct);
        await services.GetRequiredService<IUnitOfWork>().ExecuteAsync(async t =>
        {
            await SchedulingSeed.SeedAsync(scheduling, rooms, t);
            await SeedPricesAsync(billing, scheduling, t);
        }, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        await OpenDoctorDaysAsync(scheduling, services.GetRequiredService<DoctorDayService>(), today, ct);
    }

    /// <summary>Bảng giá DEV-2026: phí khám theo cấp bậc và phí các dịch vụ CLS đã có; chỉ thêm dòng còn thiếu.</summary>
    public static async Task SeedPricesAsync(BillingDbContext billing, SchedulingDbContext scheduling, CancellationToken ct)
    {
        var priceList = await billing.Set<PriceList>().FirstOrDefaultAsync(x => x.PriceListCode == PriceListCode, ct);
        if (priceList is null)
        {
            priceList = new PriceList
            {
                Id = PriceListId,
                PriceListCode = PriceListCode,
                PriceListName = "Bảng giá giả lập dev 2026",
                Currency = "VND",
                EffectiveFromUtc = new DateTime(2025, 12, 31, 17, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };
            billing.Add(priceList);
        }

        var items = await billing.Set<PriceListItem>().AsNoTracking().Where(x => x.PriceListId == priceList.Id)
            .Select(x => new { x.ItemType, x.DoctorRankId, x.DoctorId, x.MedicalServiceId }).ToListAsync(ct);
        billing.AddRange(ExamFees
            .Where(f => !items.Any(i => i.ItemType == ItemType.ConsultationFee && i.DoctorRankId == f.RankId && i.DoctorId == null))
            .Select(f => new PriceListItem
            {
                Id = Guid.NewGuid(), PriceListId = priceList.Id, ItemType = ItemType.ConsultationFee, DoctorRankId = f.RankId,
                UnitPrice = f.Fee, IsActive = true
            }));

        var codes = ClsFees.Select(x => x.ServiceCode).ToArray();
        var services = await scheduling.Set<MedicalService>().AsNoTracking().Where(x => codes.Contains(x.ServiceCode))
            .Select(x => new { x.Id, x.ServiceCode }).ToListAsync(ct);
        billing.AddRange(services
            .Where(s => !items.Any(i => i.ItemType == ItemType.CLSService && i.MedicalServiceId == s.Id))
            .Select(s => new PriceListItem
            {
                Id = Guid.NewGuid(), PriceListId = priceList.Id, ItemType = ItemType.CLSService, MedicalServiceId = s.Id,
                UnitPrice = ClsFees.Single(f => f.ServiceCode == s.ServiceCode).Fee, IsActive = true
            }));
        await billing.SaveChangesAsync(ct);
    }

    /// <summary>Mở lịch <see cref="DaysAhead"/> ngày cho bác sĩ demo; ngày đã có (kể cả đã đóng) giữ nguyên.</summary>
    public static async Task OpenDoctorDaysAsync(SchedulingDbContext scheduling, DoctorDayService days, DateOnly today,
        CancellationToken ct)
    {
        var lastDate = today.AddDays(DaysAhead - 1);
        var doctorIds = DoctorRooms.Select(x => x.DoctorId).ToArray();
        var existing = (await scheduling.Set<DoctorDayQuota>().AsNoTracking()
                .Where(x => doctorIds.Contains(x.DoctorId) && x.ServiceDate >= today && x.ServiceDate <= lastDate)
                .Select(x => new { x.DoctorId, x.ServiceDate })
                .ToListAsync(ct))
            .Select(x => (x.DoctorId, x.ServiceDate))
            .ToHashSet();

        foreach (var (doctorId, roomId) in DoctorRooms)
        {
            for (var date = today; date <= lastDate; date = date.AddDays(1))
            {
                // OpenAsync mở lại ngày đã đóng, nên chỉ gọi cho ngày chưa có.
                if (!existing.Contains((doctorId, date)))
                    await days.OpenAsync(doctorId, date, roomId, ct);
            }
        }
    }
}
