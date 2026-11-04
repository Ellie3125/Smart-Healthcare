// Cấu hình menu điều hướng theo role
// Lưu ý: Thực tế phân quyền do Backend kiểm soát; menu chỉ để UX
export interface MenuItem {
  label: string;
  icon: string;
  path: string;
}

export interface RoleMenu {
  roleLabel: string;
  roleIcon: string;
  items: MenuItem[];
}

export const ROLE_MENUS: Record<string, RoleMenu> = {
  Receptionist: {
    roleLabel: "Lễ tân",
    roleIcon: "assignment_ind",
    items: [
      { label: "Trang chủ", icon: "home", path: "/reception" },
      {
        label: "Tiếp nhận bệnh nhân",
        icon: "person_add",
        path: "/reception/check-in",
      },
      {
        label: "Danh sách lịch hẹn",
        icon: "calendar_today",
        path: "/reception/appointments",
      },
    ],
  },
  Nurse: {
    roleLabel: "Điều dưỡng",
    roleIcon: "vaccines",
    items: [
      { label: "Trang chủ", icon: "home", path: "/nurse" },
      {
        label: "Sinh hiệu bệnh nhân",
        icon: "monitor_heart",
        path: "/nurse/vitals",
      },
      { label: "Hàng chờ", icon: "queue", path: "/nurse/queue" },
    ],
  },
  Doctor: {
    roleLabel: "Bác sĩ",
    roleIcon: "stethoscope",
    items: [
      { label: "Trang chủ", icon: "home", path: "/doctor" },
      { label: "Hàng chờ khám", icon: "queue", path: "/doctor/queue" },
      { label: "Lịch sử bệnh án", icon: "history", path: "/doctor/records" },
    ],
  },
  RoomAccount: {
    roleLabel: "Phòng CLS",
    roleIcon: "biotech",
    items: [
      { label: "Trang chủ", icon: "home", path: "/room" },
      { label: "Vé dịch vụ", icon: "receipt_long", path: "/room/tickets" },
      { label: "Tải kết quả", icon: "upload_file", path: "/room/upload" },
    ],
  },
  Admin: {
    roleLabel: "Quản trị",
    roleIcon: "admin_panel_settings",
    items: [
      { label: "Trang chủ", icon: "home", path: "/admin" },
      { label: "Người dùng", icon: "manage_accounts", path: "/admin/users" },
      { label: "Danh mục", icon: "list_alt", path: "/admin/catalog" },
    ],
  },
};

/** Lấy đường dẫn gốc của role (home page sau khi đăng nhập) */
export function getRoleHomePath(role: string): string {
  const paths: Record<string, string> = {
    Receptionist: "/reception",
    Nurse: "/nurse",
    Doctor: "/doctor",
    RoomAccount: "/room",
    Admin: "/admin",
  };
  return paths[role] ?? "/";
}
