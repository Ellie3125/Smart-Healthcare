export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  currentVersion?: number;
}

export class ApiHttpError extends Error {
  status: number;
  code: string;
  detail: string;
  problem?: ProblemDetails;

  constructor(status: number, code: string, detail: string, problem?: ProblemDetails) {
    super(detail);
    this.name = 'ApiHttpError';
    this.status = status;
    this.code = code;
    this.detail = detail;
    this.problem = problem;
  }
}

const ERROR_CODE_VI: Record<string, string> = {
  SLOT_FULL: 'Bác sĩ đã hết suất khám trong ngày này.',
  QUOTA_EXCEEDED: 'Đã hết suất khám trong ngày cho bác sĩ này.',
  ALREADY_BOOKED_SAME_DAY: 'Hồ sơ này đã có lịch khám trong ngày được chọn.',
  BOOKING_CONFLICT_SAME_DAY: 'Hồ sơ này đã có lịch khám trong ngày được chọn.',
  HOLD_EXPIRED: 'Thời gian giữ chỗ 5 phút đã hết hạn. Vui lòng chọn lại suất khám.',
  PAYMENT_EXPIRED: 'Phiên thanh toán đã hết hạn. Vui lòng tạo giao dịch mới.',
  VERSION_CONFLICT: 'Dữ liệu đã được cập nhật bởi người khác. Vui lòng tải lại bản mới nhất trước khi lưu.',
  CONCURRENCY_CONFLICT: 'Dữ liệu đã thay đổi trên máy khác. Vui lòng xem bản mới nhất và thử lại.',
  ROOM_SESSION_CONFLICT: 'Phiên phòng/thiết bị này đã bị thay thế bởi lần đăng nhập khác.',
  ROOM_SESSION_REPLACED: 'Phiên phòng/thiết bị này đã bị thay thế bởi lần đăng nhập khác.',
  CHECKIN_DEADLINE_PASSED: 'Đã quá hạn check-in quy định. Vui lòng liên hệ xử lý ngoại lệ.',
  CUTOFF_PASSED: 'Đã quá mốc thời gian cho phép thao tác.',
  RESCHEDULE_CUTOFF_PASSED: 'Chỉ được đổi lịch trước 05:00 sáng ngày khám.',
  RESCHEDULE_DIFFERENT_FEE: 'Chỉ hỗ trợ đổi sang bác sĩ/ngày khám có cùng mức phí.',
  PROFILE_PENDING_VERIFICATION: 'Hồ sơ chưa được xác minh định danh tại quầy tiếp tân.',
  CLS_SERVICE_ROOM_INACTIVE: 'Dịch vụ cận lâm sàng này hiện không có phòng đang hoạt động.',
  CANNOT_VIEW_UNPUBLISHED_RESULT: 'Kết quả chỉ được hiển thị sau khi đã phát hành chính thức.',
  FORBIDDEN: 'Bạn không có quyền thực hiện thao tác này.',
  UNAUTHORIZED: 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
  NOT_FOUND: 'Không tìm thấy dữ liệu yêu cầu.',
  VALIDATION_ERROR: 'Thông tin nhập vào chưa hợp lệ. Vui lòng kiểm tra lại.',
  SERVICE_UNAVAILABLE: 'Dịch vụ tạm thời gián đoạn. Vui lòng thử lại sau ít phút.',
};

export function mapErrorCodeToMessage(code?: string, fallback?: string): string {
  if (code) {
    const normalized = code.trim().toUpperCase();
    if (ERROR_CODE_VI[normalized]) return ERROR_CODE_VI[normalized];
  }
  return fallback || 'Đã có lỗi xảy ra khi kết nối máy chủ. Vui lòng thử lại.';
}

export function extractFriendlyError(err: unknown): {
  status: number;
  code: string;
  message: string;
  isConflict: boolean;
  isSessionReplaced: boolean;
  currentVersion?: number;
} {
  if (err instanceof ApiHttpError) {
    const code = err.code || (err.status === 409 ? 'VERSION_CONFLICT' : 'UNKNOWN_ERROR');
    return {
      status: err.status,
      code,
      message: mapErrorCodeToMessage(code, err.detail),
      isConflict: err.status === 409,
      isSessionReplaced: code === 'ROOM_SESSION_REPLACED' || code === 'ROOM_SESSION_CONFLICT',
      currentVersion: err.problem?.currentVersion,
    };
  }
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const resp = (err as { response?: { status?: number; data?: ProblemDetails } }).response;
    const status = resp?.status ?? 500;
    const problem = resp?.data;
    const code = problem?.code || (status === 409 ? 'VERSION_CONFLICT' : status === 403 ? 'FORBIDDEN' : 'UNKNOWN_ERROR');
    return {
      status,
      code,
      message: mapErrorCodeToMessage(code, problem?.detail || problem?.title),
      isConflict: status === 409,
      isSessionReplaced: code === 'ROOM_SESSION_REPLACED' || code === 'ROOM_SESSION_CONFLICT',
      currentVersion: problem?.currentVersion,
    };
  }
  if (err instanceof Error) {
    return {
      status: 0,
      code: 'CLIENT_ERROR',
      message: err.message,
      isConflict: false,
      isSessionReplaced: false,
    };
  }
  return {
    status: 0,
    code: 'UNKNOWN_ERROR',
    message: 'Đã có lỗi không xác định.',
    isConflict: false,
    isSessionReplaced: false,
  };
}
