import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, expect, test, vi } from 'vitest';
import type { RouteId } from '../routes';

const useScreenPresence = vi.fn<(routeId: RouteId) => void>();
const useScreenView = vi.fn<(routeId: RouteId, projectId: string | null) => void>();
vi.mock('../realtime', () => ({ useScreenPresence, useScreenView }));

const { InstallScreen } = await import('./InstallScreen');

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  useScreenPresence.mockClear();
  useScreenView.mockClear();
});

test('installScreen_WhenRendered_ShouldReportTheScreenViewWithNoProject', () => {
  const root = createRoot(document.createElement('div'));
  act(() => root.render(<InstallScreen routeId="profile" testId="x">{null}</InstallScreen>));
  expect(useScreenView).toHaveBeenCalledTimes(1);
  expect(useScreenView).toHaveBeenCalledWith('profile', null);
  act(() => root.unmount());
});
