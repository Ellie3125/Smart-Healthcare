export interface UserProfile {
  userId: string;
  userName: string;
  role: string;
  email?: string;
  phoneNumber?: string;
}

export interface LoginResponse {
  accessToken: string;
  role: string;
  userName: string;
  userId: string;
}

export interface RegisterRequest {
  userName: string;
  email?: string;
  phoneNumber?: string;
  password: string;
  createSelfProfile?: boolean;
  selfProfile?: {
    fullName: string;
    dateOfBirth: string;
    gender: 'Male' | 'Female' | 'Other';
    nationalId?: string;
    phoneNumber: string;
    address: string;
  };
}
