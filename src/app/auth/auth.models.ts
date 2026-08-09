export interface LoginResponse {
  token: string;
  userId: string;
  email: string;
  roles: string[];
  expiresAt: string;
}

export interface AuthState {
  token: string;
  userId: string;
  email: string;
  roles: string[];
  expiresAt: string; // ISO string
}

export const TOKEN_STORAGE_KEY = 'pms_token';
export const USER_STORAGE_KEY = 'pms_user';
