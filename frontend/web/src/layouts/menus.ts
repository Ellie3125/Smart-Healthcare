export interface MenuItem {
  title: string;
  path: string;
  icon: string;
  end?: boolean;
  group?: string;
}

export const ROLE_MENUS: Record<string, MenuItem[]> = {
  Receptionist: [
    { title: 'Xác minh hồ sơ', path: '/reception', icon: 'verified_user', end: true, group: 'Tiếp đón & Hồ sơ' },
    { title: 'Check-in CCCD / QR', path: '/reception/checkin', icon: 'how_to_reg', group: 'Tiếp đón & Hồ sơ' },
    { title: 'Đăng ký tại quầy (Walk-in)', path: '/reception/walk-in', icon: 'person_add', group: 'Tiếp đón & Hồ sơ' },
    { title: 'Tìm kiếm hồ sơ & lịch hẹn', path: '/reception/search', icon: 'search', group: 'Tiếp đón & Hồ sơ' },
    { title: 'Danh sách cần gọi', path: '/reception/call-list', icon: 'call', group: 'Tiếp đón & Hồ sơ' },
    { title: 'Thu ngân tại quầy', path: '/reception/cashier', icon: 'payments', group: 'Thu ngân & Ca trực' },
    { title: 'Tra cứu giao dịch VNPAY', path: '/reception/querydr', icon: 'receipt_long', group: 'Thu ngân & Ca trực' },
    { title: 'Hủy lịch hẹn tại quầy', path: '/reception/cancel', icon: 'event_busy', group: 'Thu ngân & Ca trực' },
    { title: 'Chi hoàn tiền mặt', path: '/reception/refunds', icon: 'currency_exchange', group: 'Thu ngân & Ca trực' },
    { title: 'Đối soát & Chốt ca', path: '/reception/shift-close', icon: 'point_of_sale', group: 'Thu ngân & Ca trực' },
  ],
  Nurse: [
    { title: 'Đo sinh hiệu & Sàng lọc', path: '/nurse', icon: 'vital_signs', end: true, group: 'Điều dưỡng lâm sàng' },
    { title: 'Hàng chờ đo sinh hiệu', path: '/nurse/queue', icon: 'format_list_numbered', group: 'Điều dưỡng lâm sàng' },
  ],
  Doctor: [
    { title: 'Bàn làm việc & Hàng đợi', path: '/doctor', icon: 'stethoscope', end: true, group: 'Khám bệnh & Kê đơn' },
    { title: 'Lịch sử bệnh án & Đơn thuốc', path: '/doctor/records', icon: 'description', group: 'Khám bệnh & Kê đơn' },
  ],
  RoomAccount: [
    { title: 'Hàng đợi thực hiện CLS', path: '/room', icon: 'radiology', end: true, group: 'Cận lâm sàng' },
    { title: 'Nhập & Upload kết quả', path: '/room/upload', icon: 'upload_file', group: 'Cận lâm sàng' },
  ],
  Admin: [
    { title: 'Nhân sự & Phòng khám', path: '/admin', icon: 'admin_panel_settings', end: true, group: 'Danh mục & Lịch khám' },
    { title: 'Quầy & Bảng gọi số', path: '/admin/stations', icon: 'desk', group: 'Danh mục & Lịch khám' },
    { title: 'Suất & Phiên bác sĩ', path: '/admin/quotas', icon: 'calendar_month', group: 'Danh mục & Lịch khám' },
    { title: 'Dịch vụ CLS', path: '/admin/services', icon: 'biotech', group: 'Danh mục & Lịch khám' },
    { title: 'Bảng giá dịch vụ', path: '/admin/price-lists', icon: 'sell', group: 'Danh mục & Lịch khám' },
    { title: 'Quyền & Thiết bị', path: '/admin/devices', icon: 'security', group: 'Hệ thống & Trợ lý' },
    { title: 'Xử lý ngoại lệ', path: '/admin/exceptions', icon: 'rule_folder', group: 'Giám sát & Tài chính' },
    { title: 'Duyệt hoàn tiền', path: '/admin/refunds', icon: 'approval_delegation', group: 'Giám sát & Tài chính' },
    { title: 'Đối soát thanh toán', path: '/admin/reconcile', icon: 'account_balance', group: 'Giám sát & Tài chính' },
    { title: 'Báo cáo điều hành', path: '/admin/reports', icon: 'assessment', group: 'Giám sát & Tài chính' },
    { title: 'Nhật ký kiểm toán', path: '/admin/audit-logs', icon: 'history', group: 'Giám sát & Tài chính' },
  ],
};
