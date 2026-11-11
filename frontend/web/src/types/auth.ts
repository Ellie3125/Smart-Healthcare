export type StaffRole =
  | "Receptionist"
  | "Nurse"
  | "Doctor"
  | "RoomAccount"
  | "Admin";

export interface UserProfile {
  userId: string;
  userName: string;
  role: string;
  doctorId?: string | null;
  roomId?: string | null;
}

export interface LoginResponse {
  accessToken: string;
  role: string;
  userName: string;
  userId: string;
  doctorId?: string | null;
  roomId?: string | null;
}
