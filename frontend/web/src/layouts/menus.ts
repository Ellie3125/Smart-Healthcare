export interface MenuItem {
  title: string;
  path: string;
  icon: string;
}

export const ROLE_MENUS: Record<string, MenuItem[]> = {
  Receptionist: [
    { title: 'Tiếp đón & Check-in', path: '/reception', icon: 'how_to_reg' },
    { title: 'Hàng chờ tiếp đón', path: '/reception/queue', icon: 'format_list_numbered' },
  ],
  Nurse: [
    { title: 'Đo sinh hiệu & Phân luồng', path: '/nurse', icon: 'vital_signs' },
    { title: 'Danh sách chờ điều dưỡng', path: '/nurse/queue', icon: 'medical_information' },
  ],
  Doctor: [
    { title: 'Phòng khám bác sĩ', path: '/doctor', icon: 'stethoscope' },
    { title: 'Bệnh án & Đơn thuốc', path: '/doctor/records', icon: 'description' },
  ],
  RoomAccount: [
    { title: 'Thực hiện dịch vụ CLS', path: '/room', icon: 'radiology' },
    { title: 'Tải lên kết quả tệp', path: '/room/upload', icon: 'upload_file' },
  ],
  Admin: [
    { title: 'Quản trị hệ thống', path: '/admin', icon: 'admin_panel_settings' },
    { title: 'Nhật ký & Vận hành', path: '/admin/logs', icon: 'terminal' },
  ],
};

