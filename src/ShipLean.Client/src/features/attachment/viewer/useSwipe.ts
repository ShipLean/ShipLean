import { useRef, useState, type PointerEvent } from 'react';

/** A sideways drag longer than this (px) changes image on release. */
export const swipeDistance = 60;
/** A sideways flick faster than this (px per ms) changes image on release, however short. */
export const swipeSpeed = 0.4;

/** Whether a drag of `dx` px over `ms` milliseconds goes on to the next image (1), the previous one (-1) or neither (0). */
export function swipeDirection(dx: number, ms: number): -1 | 0 | 1 {
  const far = Math.abs(dx) >= swipeDistance;
  const fast = ms > 0 && Math.abs(dx) / ms > swipeSpeed && Math.abs(dx) > 20;
  if (!far && !fast) return 0;
  return dx < 0 ? 1 : -1;
}

/**
 * One-finger sideways swipe between images. Off while `enabled` is false (zoomed in, where a drag pans) and cancelled
 * when a second finger lands (a pinch). `offset` is how far the image has followed the finger.
 */
export function useSwipe(enabled: boolean, onSwipe: (direction: 1 | -1) => void) {
  const [offset, setOffset] = useState(0);
  const start = useRef<{ id: number; x: number; time: number } | null>(null);
  const fingers = useRef(new Set<number>());

  const finish = (event: PointerEvent<HTMLElement>, cancelled: boolean) => {
    fingers.current.delete(event.pointerId);
    const current = start.current;
    if (!current || current.id !== event.pointerId) return;
    start.current = null;
    setOffset(0);
    if (cancelled || !enabled) return;
    const direction = swipeDirection(event.clientX - current.x, event.timeStamp - current.time);
    if (direction !== 0) onSwipe(direction);
  };

  return {
    offset,
    dragging: offset !== 0,
    handlers: {
      onPointerDown: (event: PointerEvent<HTMLElement>) => {
        fingers.current.add(event.pointerId);
        if (fingers.current.size > 1) {
          start.current = null;
          setOffset(0);
        } else if (enabled) {
          start.current = { id: event.pointerId, x: event.clientX, time: event.timeStamp };
        }
      },
      onPointerMove: (event: PointerEvent<HTMLElement>) => {
        if (start.current?.id === event.pointerId && enabled) {
          setOffset(event.clientX - start.current.x);
        }
      },
      onPointerUp: (event: PointerEvent<HTMLElement>) => finish(event, false),
      onPointerCancel: (event: PointerEvent<HTMLElement>) => finish(event, true),
    },
  };
}
