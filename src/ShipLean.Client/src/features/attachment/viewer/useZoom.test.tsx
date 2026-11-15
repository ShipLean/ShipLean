import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeAll, expect, it } from 'vitest';
import { useZoom } from './useZoom';

beforeAll(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
});

// The hook's latest return value, without a testing library.
function renderHook() {
  const result = {} as { current: ReturnType<typeof useZoom> };
  function Probe() {
    result.current = useZoom();
    return null;
  }
  act(() => createRoot(document.createElement('div')).render(<Probe />));
  return { result };
}

type Handlers = ReturnType<typeof useZoom>['handlers'];
type PointerArg = Parameters<Handlers['onPointerDown']>[0];

const pointer = (pointerId: number, clientX: number, clientY: number, timeStamp: number) =>
  ({
    pointerId,
    clientX,
    clientY,
    timeStamp,
    currentTarget: {
      setPointerCapture: () => undefined,
      getBoundingClientRect: () => ({ width: 400, height: 600 }),
    },
  }) as unknown as PointerArg;

const tap = (hook: { current: ReturnType<typeof useZoom> }, time: number) => {
  act(() => hook.current.handlers.onPointerDown(pointer(1, 100, 100, time)));
  act(() => hook.current.handlers.onPointerUp(pointer(1, 100, 100, time + 40)));
};

it('useZoom_WhenDoubleTapped_ShouldZoomToTwiceAndBack', () => {
  const { result } = renderHook();
  expect(result.current.scale).toBe(1);

  tap(result, 0);
  expect(result.current.scale).toBe(1);
  tap(result, 150);
  expect(result.current.scale).toBe(2);
  expect(result.current.zoomed).toBe(true);

  tap(result, 1000);
  tap(result, 1150);
  expect(result.current.scale).toBe(1);
  expect(result.current.zoomed).toBe(false);
});

it('useZoom_WhenTapsAreFarApart_ShouldNotZoom', () => {
  const { result } = renderHook();
  tap(result, 0);
  tap(result, 900);
  expect(result.current.scale).toBe(1);
});

it('useZoom_WhenPinched_ShouldClampScale', () => {
  const { result } = renderHook();
  const h = () => result.current.handlers;
  act(() => h().onPointerDown(pointer(1, 100, 100, 0)));
  act(() => h().onPointerDown(pointer(2, 200, 100, 0)));

  // Spread to ten times the start distance: held at 4.
  act(() => h().onPointerMove(pointer(2, 1100, 100, 10)));
  expect(result.current.scale).toBe(4);

  // Pinched in past the start: held at 1, not shrunk.
  act(() => h().onPointerMove(pointer(2, 120, 100, 20)));
  expect(result.current.scale).toBe(1);
  expect(result.current.x).toBe(0);
});

it('useZoom_WhenDraggedWhileZoomed_ShouldPanWithinTheImage', () => {
  const { result } = renderHook();
  tap(result, 0);
  tap(result, 150);
  act(() => result.current.handlers.onPointerDown(pointer(1, 100, 100, 2000)));
  act(() => result.current.handlers.onPointerMove(pointer(1, 900, 100, 2100)));
  // Scale 2 on a 400px stage allows 200px of pan.
  expect(result.current.x).toBe(200);
});
