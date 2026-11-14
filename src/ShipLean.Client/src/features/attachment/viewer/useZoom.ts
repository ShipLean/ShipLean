import { useCallback, useRef, useState, type PointerEvent } from 'react';

export const maxScale = 4;
export const doubleTapScale = 2;
/** Two taps closer together than this (ms) are a double tap. */
export const doubleTapMs = 300;
/** A press that moves more than this (px) is a drag, not a tap. */
const tapSlop = 10;

export const clampScale = (scale: number) => Math.min(Math.max(scale, 1), maxScale);

type Point = { x: number; y: number };
export type ZoomState = { scale: number; x: number; y: number };
const unzoomed: ZoomState = { scale: 1, x: 0, y: 0 };

const distance = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y);

/**
 * Pinch and double-tap zoom for one image (the viewer, SHIP-715 D4), and panning while zoomed. Spread the returned
 * `handlers` on the element the gestures happen on. Pointer events only, so a mouse works the same as fingers.
 */
export function useZoom() {
  const [state, setState] = useState<ZoomState>(unzoomed);
  const pointers = useRef(new Map<number, Point>());
  const pinch = useRef<{ distance: number; scale: number } | null>(null);
  const press = useRef<{ at: Point; time: number } | null>(null);
  const lastTap = useRef<{ at: Point; time: number } | null>(null);
  const scaleRef = useRef(1);

  const apply = useCallback((next: ZoomState) => {
    scaleRef.current = next.scale;
    setState(next);
  }, []);

  const reset = useCallback(() => {
    pointers.current.clear();
    pinch.current = null;
    press.current = null;
    lastTap.current = null;
    apply(unzoomed);
  }, [apply]);

  const onPointerDown = (event: PointerEvent<HTMLElement>) => {
    const point = { x: event.clientX, y: event.clientY };
    pointers.current.set(event.pointerId, point);
    event.currentTarget.setPointerCapture?.(event.pointerId);
    if (pointers.current.size === 2) {
      const [a, b] = [...pointers.current.values()];
      pinch.current = a && b ? { distance: distance(a, b), scale: scaleRef.current } : null;
      press.current = null;
      lastTap.current = null;
    } else if (pointers.current.size === 1) {
      press.current = { at: point, time: event.timeStamp };
    }
  };

  const onPointerMove = (event: PointerEvent<HTMLElement>) => {
    const before = pointers.current.get(event.pointerId);
    if (!before) return;
    const point = { x: event.clientX, y: event.clientY };
    pointers.current.set(event.pointerId, point);
    if (press.current && distance(press.current.at, point) > tapSlop) press.current = null;
    if (pointers.current.size === 2 && pinch.current) {
      const [a, b] = [...pointers.current.values()];
      if (!a || !b || pinch.current.distance === 0) return;
      const scale = clampScale((pinch.current.scale * distance(a, b)) / pinch.current.distance);
      apply(scale === 1 ? unzoomed : { ...state, scale });
    } else if (pointers.current.size === 1 && scaleRef.current > 1) {
      const rect = event.currentTarget.getBoundingClientRect();
      const limitX = ((scaleRef.current - 1) * rect.width) / 2;
      const limitY = ((scaleRef.current - 1) * rect.height) / 2;
      const clamp = (value: number, limit: number) => Math.min(Math.max(value, -limit), limit);
      apply({
        scale: scaleRef.current,
        x: clamp(state.x + point.x - before.x, limitX),
        y: clamp(state.y + point.y - before.y, limitY),
      });
    }
  };

  const release = (event: PointerEvent<HTMLElement>, cancelled: boolean) => {
    if (!pointers.current.delete(event.pointerId)) return;
    if (pointers.current.size < 2) pinch.current = null;
    if (scaleRef.current < 1.05) apply(unzoomed);
    const tapped = press.current;
    press.current = null;
    if (cancelled || !tapped || event.timeStamp - tapped.time > doubleTapMs) return;
    const previous = lastTap.current;
    if (previous && event.timeStamp - previous.time < doubleTapMs) {
      lastTap.current = null;
      apply(scaleRef.current > 1 ? unzoomed : { scale: doubleTapScale, x: 0, y: 0 });
    } else {
      lastTap.current = { at: tapped.at, time: event.timeStamp };
    }
  };

  return {
    ...state,
    zoomed: state.scale > 1,
    reset,
    handlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp: (event: PointerEvent<HTMLElement>) => release(event, false),
      onPointerCancel: (event: PointerEvent<HTMLElement>) => release(event, true),
    },
  };
}
