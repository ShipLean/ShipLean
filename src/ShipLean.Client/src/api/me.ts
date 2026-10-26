// The signed-in user. C#: ShipLean.Contracts.Users.MeDto.
import type { UserDto } from './users';

export const THEMES = ['os', 'light', 'dark'] as const;
export type Theme = (typeof THEMES)[number];

export const DENSITIES = ['compact', 'comfortable'] as const;
export type Density = (typeof DENSITIES)[number];

export interface MeDto extends UserDto {
  hasProjects: boolean;
  canCreateProjects: boolean;
  theme: Theme;
  density: Density;
}
