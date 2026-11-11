export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export class PatientApiError extends Error {
  status: number;
  code: string;
  detail: string;
  problem?: ProblemDetails;

  constructor(status: number, code: string, detail: string, problem?: ProblemDetails) {
    super(detail);
    this.name = 'PatientApiError';
    this.status = status;
    this.code = code;
    this.detail = detail;
    this.problem = problem;
  }
}

const PATIENT_ERROR_MAP: Record<string, string> = {
  SLOT_FULL: 'Bác sĩ đã hết suất khám trong ngày này. Vui lòng chọn ngày hoặc bác sĩ khác.',
  QUOTA_EXCEEDED: 'Bác sĩ đã hết suất khám trong ngày này. Vui lòng chọn ngày hoặc bác sĩ khác.',
  ALREADY_BOOKED_SAME_DAY: 'Hồ sơ này đã có lịch khám trong cùng ngày. Mỗi người chỉ giữ tối đa một lịch trong ngày.',
  BOOKING_CONFLICT_SAME_DAY: 'Hồ sơ này đã có lịch khám trong cùng ngày.',
  HOLD_EXPIRED: 'Phiên giữ chỗ 5 phút đã hết hạn. Vui lòng đặt lại từ đầu.',
  PAYMENT_EXPIRED: 'Phiên thanh toán đã hết hạn. Vui lòng tạo lại yêu cầu thanh toán.',
  RESCHEDULE_CUTOFF_PASSED: 'Đã quá mốc 05:00 sáng ngày khám nên không thể đổi lịch trực tuyến.',
  RESCHEDULE_DIFFERENT_FEE: 'Chỉ hỗ trợ đổi sang bác sĩ hoặc ngày khám có cùng mức phí khám.',
  PROFILE_PENDING_VERIFICATION: 'Hồ sơ chưa được xác minh định danh. Vui lòng mang giấy tờ tùy thân khi đến khám để hoàn tất xác minh.',
  VERSION_CONFLICT: 'Dữ liệu vừa được cập nhật. Vui lòng tải lại trang.',
  UNAUTHORIZED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
  FORBIDDEN: 'Bạn không có quyền truy cập nội dung này.',
  NOT_FOUND: 'Không tìm thấy thông tin bạn đang tìm.',
  SERVICE_UNAVAILABLE: 'Dịch vụ tạm thời gián đoạn. Vui lòng thử lại sau ít phút.',
};

export function mapPatientErrorCode(code?: string, fallback?: string): string {
  if (code) {
    const key = code.trim().toUpperCase();
    if (PATIENT_ERROR_MAP[key]) return PATIENT_ERROR_MAP[key];
  }
  return fallback || 'Không thể kết nối máy chủ lúc này. Vui lòng thử lại.';
}

export function extractPatientError(err: unknown): {
  status: number;
  code: string;
  message: string;
  isPendingVerification: boolean;
} {
  if (err instanceof PatientApiError) {
    return {
      status: err.status,
      code: err.code,
      message: mapPatientErrorCode(err.code, err.detail),
      isPendingVerification: err.code === 'PROFILE_PENDING_VERIFICATION',
    };
  }
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const resp = (err as { response?: { status?: number; data?: ProblemDetails } }).response;
    const status = resp?.status ?? 500;
    const problem = resp?.data;
    const code = (problem?.code || (status === 403 ? 'FORBIDDEN' : status === 401 ? 'UNAUTHORIZED' : 'HTTP_ERROR')).toUpperCase();
    return {
      status,
      code,
      message: mapPatientErrorCode(code, problem?.detail || problem?.title),
      isPendingVerification: code === 'PROFILE_PENDING_VERIFICATION',
    };
  }
  if (err instanceof Error) {
    return { status: 0, code: 'CLIENT_ERROR', message: err.message, isPendingVerification: false };
  }
  return { status: 0, code: 'UNKNOWN', message: 'Đã xảy ra lỗi không xác định.', isPendingVerification: false };
}
