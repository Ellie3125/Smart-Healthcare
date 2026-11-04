// Định nghĩa vai trò cho kênh Web Nhân viên
// Lưu ý: Role "Patient" KHÔNG nằm trong danh sách này vì bệnh nhân chỉ dùng mobile app
export type StaffRole = 'Receptionist' | 'Nurse' | 'Doctor' | 'RoomAccount' | 'Admin';

export interface CurrentUser {
  id: string;
  userName: string;
  role: string;
  doctorId?: string | null;
  roomId?: string | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresInSeconds: number;
  user: CurrentUser;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
}

