using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;

namespace IdentityPatients.Domain.StateMachines;

public sealed record VerificationTransitionOutcome(
    bool Succeeded,
    bool IsIdempotent,
    VerificationStatus OldStatus,
    VerificationStatus NewStatus,
    bool OldIsActive,
    bool NewIsActive,
    string? ErrorCode = null,
    string? ErrorDetail = null,
    string[]? AllowedTransitions = null);

/// <summary>
/// State machine duy nhất quản lý chuyển trạng thái VerificationStatus của PatientRepresentative.
/// Mọi chuyển trạng thái phải đi qua lớp này để đảm bảo nhất quán và idempotent.
/// </summary>
public static class VerificationStateMachine
{
    public const string InvalidTransitionCode = "InvalidStatusTransition";

    public static string[] GetAllowedTransitions(VerificationStatus currentStatus) => currentStatus switch
    {
        VerificationStatus.Pending => ["Pending -> Verified", "Pending -> Rejected"],
        VerificationStatus.Verified => ["Verified -> Revoked"],
        VerificationStatus.Rejected => ["Rejected -> Pending"],
        VerificationStatus.Revoked => [],
        _ => []
    };

    public static VerificationTransitionOutcome ApplyTransition(
        PatientRepresentative representative,
        VerificationStatus targetStatus,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(representative);

        var oldStatus = representative.VerificationStatus;
        var oldIsActive = representative.IsActive;

        // Idempotent: gọi lại đúng trạng thái hiện tại thì không thay đổi dữ liệu và không ghi thêm AuditLog.
        // Lưu ý: nếu đang Pending nhưng đã chủ động unlink (IsActive = false) thì không được coi là hợp lệ để resubmit.
        if (oldStatus == targetStatus)
        {
            if (targetStatus == VerificationStatus.Pending && !oldIsActive)
            {
                return Invalid(oldStatus, oldIsActive, targetStatus);
            }

            return new VerificationTransitionOutcome(
                Succeeded: true,
                IsIdempotent: true,
                OldStatus: oldStatus,
                NewStatus: oldStatus,
                OldIsActive: oldIsActive,
                NewIsActive: oldIsActive);
        }

        switch (oldStatus, targetStatus)
        {
            case (VerificationStatus.Pending, VerificationStatus.Verified):
                representative.VerificationStatus = VerificationStatus.Verified;
                representative.IsActive = true;
                representative.RevokedAtUtc = null;
                break;

            case (VerificationStatus.Pending, VerificationStatus.Rejected):
                representative.VerificationStatus = VerificationStatus.Rejected;
                representative.IsActive = false;
                representative.RevokedAtUtc = utcNow;
                break;

            case (VerificationStatus.Verified, VerificationStatus.Revoked):
                representative.VerificationStatus = VerificationStatus.Revoked;
                representative.IsActive = false;
                representative.RevokedAtUtc = utcNow;
                break;

            case (VerificationStatus.Rejected, VerificationStatus.Pending):
                representative.VerificationStatus = VerificationStatus.Pending;
                representative.IsActive = true;
                representative.RevokedAtUtc = null;
                break;

            default:
                return Invalid(oldStatus, oldIsActive, targetStatus);
        }

        return new VerificationTransitionOutcome(
            Succeeded: true,
            IsIdempotent: false,
            OldStatus: oldStatus,
            NewStatus: representative.VerificationStatus,
            OldIsActive: oldIsActive,
            NewIsActive: representative.IsActive);
    }

    private static VerificationTransitionOutcome Invalid(
        VerificationStatus currentStatus,
        bool isActive,
        VerificationStatus targetStatus)
    {
        return new VerificationTransitionOutcome(
            Succeeded: false,
            IsIdempotent: false,
            OldStatus: currentStatus,
            NewStatus: currentStatus,
            OldIsActive: isActive,
            NewIsActive: isActive,
            ErrorCode: InvalidTransitionCode,
            ErrorDetail: $"Không thể chuyển trạng thái xác minh từ '{currentStatus}' sang '{targetStatus}'.",
            AllowedTransitions: GetAllowedTransitions(currentStatus));
    }
}
