import { api } from '../../api/client';
import type { MeDto } from '../../api/me';

// C#: ShipLean.Contracts.Auth.InstanceDto.
export interface InstanceDto {
  name: string;
  /** C#: ShipLean.Contracts.Auth.SignInMode. */
  signIn: 'password' | 'proxy';
  /** Where "Sign out" goes in proxy mode; null hides it (and always null in password mode). */
  signOutUrl: string | null;
}

export const instanceKey = ['auth', 'instance'] as const;

export const fetchInstance = () => api.get<InstanceDto>('/api/auth/instance');

export const login = (email: string, password: string) =>
  api.post<MeDto>('/api/auth/login', { email, password });

export const changePassword = (current: string, next: string) =>
  api.post<MeDto>('/api/auth/password', { current, new: next });

export const logout = () => api.post<void>('/api/auth/logout');
