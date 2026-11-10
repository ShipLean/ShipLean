// Fake data, starting from the pinned wire shapes (src/ShipLean.TestSupport/DtoFixtures) where there is one.
import type { DeviceSessionDto, DeviceSignInDto } from '@shiplean/core/api/deviceSessions';
import type { HealthDto } from '@shiplean/core/api/health';
import type { MeDto } from '@shiplean/core/api/me';
import type { ProjectSummaryDto } from '@shiplean/core/api/projects';
import deviceSignIn from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/device-sign-in.json';

// The server every signed-in test talks to.
export const TEST_ORIGIN = 'https://shiplean.test';
export const TEST_SECRET = 'shl_dev_TestSecret0000000000000000000000000000000';

const fixture = deviceSignIn as DeviceSignInDto;

export function aMe(overrides: Partial<MeDto> = {}): MeDto {
  return { ...fixture.me, ...overrides };
}

export function aProject(overrides: Partial<ProjectSummaryDto> = {}): ProjectSummaryDto {
  return {
    id: '0199f000-0000-7000-8000-0000000000a1',
    key: 'SHIP',
    name: 'ShipLean',
    role: 'member',
    memberCount: 3,
    ...overrides,
  };
}

export function aHealth(overrides: Partial<HealthDto> = {}): HealthDto {
  return { status: 'ok', version: '1.4.0+7f26ef0a', search: null, ...overrides };
}

export function aDeviceSession(overrides: Partial<DeviceSessionDto> = {}): DeviceSessionDto {
  return { ...fixture.session, ...overrides };
}

export function aDeviceSignIn(overrides: Partial<DeviceSignInDto> = {}): DeviceSignInDto {
  return { ...fixture, secret: TEST_SECRET, ...overrides };
}
