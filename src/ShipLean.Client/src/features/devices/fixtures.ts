import type { DeviceSessionDto } from '@shiplean/core/api/deviceSessions';

// Two phones as the server lists them: labels are the server's own words ("2h ago", "3 Nov").
export const pixel: DeviceSessionDto = {
  id: '115e7dbf-e64a-4acf-a5c3-554df2c6e74b',
  name: 'Pixel 8 Pro (work)',
  tokenPrefix: 'shl_dev_P7mR2wQz',
  createdAt: '2026-09-14T17:40:00Z',
  createdLabel: '14 Sep',
  lastUsedAt: '2026-10-23T19:30:00Z',
  lastUsedLabel: 'Yesterday',
  expiresAt: '2027-01-21T19:30:00Z',
  current: false,
};

export const iphone: DeviceSessionDto = {
  id: '6b0f1c52-3a0e-4a38-9d0e-0f3c2b7a9e11',
  name: 'Priya’s iPhone',
  tokenPrefix: 'shl_dev_k3Xq9TbA',
  createdAt: '2026-11-03T08:10:00Z',
  createdLabel: '3 Nov',
  lastUsedAt: '2026-11-09T07:00:00Z',
  lastUsedLabel: '2h ago',
  expiresAt: '2027-02-01T08:10:00Z',
  current: false,
};

export const twoDevices: DeviceSessionDto[] = [iphone, pixel];
