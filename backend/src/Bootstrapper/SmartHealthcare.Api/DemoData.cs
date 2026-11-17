using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Billing.Infrastructure.Persistence;
using IdentityPatients.Application.Common;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace SmartHealthcare.Api;

/// <summary>
/// Dữ liệu demo để đặt thử ngay sau khi deploy: bảng giá phí khám GIẢ theo cấp bậc và lịch khám 7 ngày tới cho bác sĩ
/// phòng khám đã seed ở IdentityPatients. Bật bằng cờ <c>Seed:DemoData</c> (Compose: <c>SEED_DEMO_DATA</c>);
/// Development mặc định bật, môi trường khác mặc định tắt. Prod phải để false.
/// </summary>
public static class DemoData
{
    public const string Flag = "Seed:DemoData";
    // Cùng mã, Id và giá với infrastructure/sqlserver/seed-dev.sql (db-seed của Docker Compose dev) để hai nguồn seed
    // không sinh hai bảng giá cùng hiệu lực: báo giá lỗi khi một cấp bậc có hai mức phí.
    public const string PriceListCode = "DEV-2026";
    private static readonly Guid PriceListId = Guid.Parse("0a1f0004-0000-4000-8000-000000000001");
    public const string ClsPriceListCode = "DEV-CLS-2026";
    private static readonly Guid ClsPriceListId = Guid.Parse("0a1f0004-0000-4000-8000-000000000002");
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

    // Dịch vụ Cận lâm sàng (CLS) mẫu nối giữa Scheduling, Billing và các phòng CLS của IdentityPatients
    public static readonly (Guid Id, string Code, string Name, ServiceCategory Category, Guid DefaultRoomId, decimal Fee)[] DemoClsServices =
    [
        (Guid.Parse("7a1f0001-0000-4000-8000-000000000001"), "DEV-XQ-NGUC", "Chụp X-Quang ngực thẳng kỹ thuật số", ServiceCategory.Radiology, SeedConstants.RoomXQ01Id, 120_000m),
        (Guid.Parse("7a1f0001-0000-4000-8000-000000000002"), "DEV-XN-CTM", "Tổng phân tích tế bào máu ngoại vi (Công thức máu)", ServiceCategory.Laboratory, SeedConstants.RoomXN01Id, 80_000m),
        (Guid.Parse("7a1f0001-0000-4000-8000-000000000003"), "DEV-XN-SH", "Sinh hóa máu cơ bản (Glucose, Ure, Creatinin)", ServiceCategory.Laboratory, SeedConstants.RoomXN01Id, 150_000m),
        (Guid.Parse("7a1f0001-0000-4000-8000-000000000004"), "DEV-SA-BUNG", "Siêu âm ổ bụng tổng quát màu", ServiceCategory.Ultrasound, SeedConstants.RoomSA01Id, 180_000m),
        (Guid.Parse("7a1f0001-0000-4000-8000-000000000005"), "DEV-NS-DD", "Nội soi thực quản - dạ dày - tá tràng", ServiceCategory.Endoscopy, SeedConstants.RoomNS01Id, 450_000m),
    ];

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue(Flag, environment.IsDevelopment());

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

        var pricedRanks = await billing.Set<PriceListItem>()
            .Where(x => x.PriceListId == priceList.Id && x.ItemType == ItemType.ConsultationFee && x.DoctorId == null)
            .Select(x => x.DoctorRankId)
            .ToListAsync(ct);
        billing.AddRange(ExamFees.Where(x => !pricedRanks.Contains(x.RankId)).Select(x => new PriceListItem
        {
            Id = Guid.NewGuid(),
            PriceListId = priceList.Id,
            ItemType = ItemType.ConsultationFee,
            DoctorRankId = x.RankId,
            UnitPrice = x.Fee,
            IsActive = true
        }));

        // Seed dịch vụ Cận lâm sàng (CLS) trong Scheduling (idempotent)
        var existingServiceIds = await scheduling.Set<MedicalService>()
            .Select(s => s.Id)
            .ToListAsync(ct);

        foreach (var svc in DemoClsServices)
        {
            if (!existingServiceIds.Contains(svc.Id))
            {
                scheduling.Add(new MedicalService
                {
                    Id = svc.Id,
                    ServiceCode = svc.Code,
                    ServiceName = svc.Name,
                    ServiceCategory = svc.Category,
                    DefaultRoomId = svc.DefaultRoomId,
                    IsMandatoryDefault = false,
                    IsActive = true,
                });

                scheduling.Add(new MedicalServiceRoom
                {
                    Id = Guid.NewGuid(),
                    MedicalServiceId = svc.Id,
                    RoomId = svc.DefaultRoomId,
                    IsDefaultRoom = true,
                    IsActive = true,
                });
            }
        }
        await scheduling.SaveChangesAsync(ct);

        // Seed giá dịch vụ CLS trong PriceList riêng của Billing để không xung đột với PriceList DEV-2026 (idempotent)
        var clsPriceList = await billing.Set<PriceList>().FirstOrDefaultAsync(x => x.PriceListCode == ClsPriceListCode, ct);
        if (clsPriceList is null)
        {
            clsPriceList = new PriceList
            {
                Id = ClsPriceListId,
                PriceListCode = ClsPriceListCode,
                PriceListName = "Bảng giá Cận lâm sàng giả lập dev 2026",
                Currency = "VND",
                EffectiveFromUtc = new DateTime(2025, 12, 31, 17, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };
            billing.Add(clsPriceList);
        }

        var pricedServices = await billing.Set<PriceListItem>()
            .Where(x => x.PriceListId == clsPriceList.Id && x.ItemType == ItemType.CLSService)
            .Select(x => x.MedicalServiceId)
            .ToListAsync(ct);

        billing.AddRange(DemoClsServices.Where(x => !pricedServices.Contains(x.Id)).Select(x => new PriceListItem
        {
            Id = Guid.NewGuid(),
            PriceListId = clsPriceList.Id,
            ItemType = ItemType.CLSService,
            MedicalServiceId = x.Id,
            UnitPrice = x.Fee,
            IsActive = true
        }));

        await billing.SaveChangesAsync(ct);

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
                {
                    await days.OpenAsync(doctorId, date, roomId, ct);
                }
            }
        }
    }
}
