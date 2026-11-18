using IdentityPatients.Application.Administration;
using IdentityPatients.Application.Common;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

/// <summary>
/// Danh mục chuyên khoa và cấp bậc bác sĩ cho bộ lọc tìm bác sĩ của web bệnh nhân (dùng với GET /api/scheduling/patient/doctors).
/// Chỉ mục đang hoạt động, chỉ đọc; quản trị vẫn ở /api/staff/admin.
/// </summary>
[Authorize(Policy = Policies.PatientOnly)]
[Route("api/patient")]
public sealed class PatientCatalogController(IStaffAdminService adminService) : ApiControllerBase
{
    [HttpGet("specialties")]
    [ProducesResponseType(typeof(IReadOnlyList<SpecialtyOption>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpecialties(CancellationToken ct) => HandleResult(Map(await adminService.GetSpecialtiesAsync(ct),
        items => items.Where(x => x.IsActive).OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .Select(x => new SpecialtyOption(x.Id, x.Code, x.Name)).ToList()));

    [HttpGet("doctor-ranks")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorRankOption>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorRanks(CancellationToken ct) => HandleResult(Map(await adminService.GetDoctorRanksAsync(ct),
        items => items.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name, StringComparer.CurrentCulture)
            .Select(x => new DoctorRankOption(x.Id, x.Code, x.Name, x.DisplayOrder)).ToList()));

    private static ServiceResult<IReadOnlyList<TOut>> Map<TIn, TOut>(ServiceResult<IReadOnlyList<TIn>> result,
        Func<IReadOnlyList<TIn>, List<TOut>> map) => result.IsSuccess
        ? ServiceResult<IReadOnlyList<TOut>>.Success(map(result.Value!))
        : throw new InvalidOperationException($"Đọc danh mục thất bại: {result.ErrorCode}");

    public sealed record SpecialtyOption(Guid Id, string Code, string Name);

    public sealed record DoctorRankOption(Guid Id, string Code, string Name, int DisplayOrder);
}
