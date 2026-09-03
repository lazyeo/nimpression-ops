export type { UserRole } from '../api/models/api-models';
import type { UserRole } from '../api/models/api-models';

export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  locale: string;
  avatarKey?: string | null;
}

export interface AuthSuccessResponse {
  accessToken: string;
  expiresIn: number;
  tokenType: string;
  user: AuthUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken?: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
