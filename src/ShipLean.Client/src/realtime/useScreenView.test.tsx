import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, expect, test, vi } from 'vitest';
import type { BacklogMode, RouteId } from '../routes';

const postScreenView =
  vi.fn<(body: { screen: string; projectId: string | null; mode?: string }) => Promise<void>>();
vi.mock('../api/usageApi', () => ({ postScreenView }));

const { useScreenView } = await import('./useScreenView');

function Probe({
  routeId,
  projectId,
  mode,
}: {
  routeId: RouteId;
  projectId: string | null;
  mode?: BacklogMode;
}) {
  useScreenView(routeId, projectId, mode);
  return null;
}

function mount(routeId: RouteId, projectId: string | null) {
  const root = createRoot(document.createElement('div'));
  act(() => root.render(<Probe routeId={routeId} projectId={projectId} />));
  return root;
}

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  postScreenView.mockReset();
  postScreenView.mockResolvedValue(undefined);
});

test('useScreenView_WhenMounted_ShouldPostOnce', () => {
  const root = mount('taskboard', 'p1');
  expect(postScreenView).toHaveBeenCalledTimes(1);
  expect(postScreenView).toHaveBeenCalledWith({ screen: 'taskboard', projectId: 'p1' });
  act(() => root.unmount());
});

test('useScreenView_WhenRouteChanges_ShouldPostAgain', () => {
  const root = mount('taskboard', 'p1');
  act(() => root.render(<Probe routeId="capacity" projectId="p1" />));
  act(() => root.render(<Probe routeId="capacity" projectId="p2" />));
  expect(postScreenView).toHaveBeenCalledTimes(3);
  expect(postScreenView).toHaveBeenLastCalledWith({ screen: 'capacity', projectId: 'p2' });
  act(() => root.unmount());
});

test('useScreenView_WhenBacklogModeChanges_ShouldPostAgainWithTheMode', () => {
  const root = createRoot(document.createElement('div'));
  act(() => root.render(<Probe routeId="featuresBacklog" projectId="p1" mode="list" />));
  act(() => root.render(<Probe routeId="featuresBacklog" projectId="p1" mode="tree" />));
  act(() => root.render(<Probe routeId="featuresBacklog" projectId="p1" mode="tree" />));
  expect(postScreenView).toHaveBeenCalledTimes(2);
  expect(postScreenView).toHaveBeenLastCalledWith({
    screen: 'featuresBacklog',
    projectId: 'p1',
    mode: 'tree',
  });
  act(() => root.unmount());
});

test('useScreenView_WhenRerenderedWithSameRoute_ShouldNotPost', () => {
  const root = mount('profile', null);
  act(() => root.render(<Probe routeId="profile" projectId={null} />));
  expect(postScreenView).toHaveBeenCalledTimes(1);
  act(() => root.unmount());
});

test('useScreenView_WhenPostFails_ShouldNotThrow', async () => {
  postScreenView.mockRejectedValue(new Error('offline'));
  const unhandled = vi.fn();
  process.on('unhandledRejection', unhandled);
  const root = mount('profile', null);
  await act(() => Promise.resolve());
  expect(postScreenView).toHaveBeenCalledTimes(1);
  expect(unhandled).not.toHaveBeenCalled();
  process.off('unhandledRejection', unhandled);
  act(() => root.unmount());
});
