// A user as the instance's user pages see them. C#: ShipLean.Contracts.Users. Nothing is omitted: absent values are null.

export const USER_STATUSES = [
  'active',
  'oneTimePassword',
  'locked',
  'noPassword',
  'disabled',
] as const;
/** Derived by the server, first match wins: disabled, noPassword, locked, oneTimePassword, active. */
export type UserStatus = (typeof USER_STATUSES)[number];

export interface UserDto {
  id: string;
  name: string;
  email: string;
  initials: string;
  /** Lowercase #rrggbb. */
  avatarColour: string;
  photoUrl: string | null;
  isInstanceAdmin: boolean;
  status: UserStatus;
}

/** The password an admin passes on; the server never shows it again. */
export interface OneTimePasswordDto {
  userId: string;
  password: string;
}
