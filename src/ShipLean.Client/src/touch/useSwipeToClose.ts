import { useRef, useState, type TouchEvent } from 'react';
import { shouldCloseOnRelease } from './swipe';

/**
 * Swipe down to close a bottom sheet. Spread `handlers` on the grab bar and head only (with `touch-action: none`
 * there), so the sheet's body still scrolls. `offset` is how far the sheet follows the finger downwards; on release it
 * closes or snaps back to 0.
 */
export function useSwipeToClose(onClose: () => void) {
  const [offset, setOffset] = useState(0);
  const drag = useRef<{ startY: number; startMs: number; y: number } | null>(null);

  const onTouchStart = (event: TouchEvent) => {
    const touch = event.touches[0];
    if (!touch || event.touches.length > 1) return;
    drag.current = { startY: touch.clientY, startMs: performance.now(), y: touch.clientY };
  };
  const onTouchMove = (event: TouchEvent) => {
    const touch = event.touches[0];
    if (!drag.current || !touch) return;
    drag.current.y = touch.clientY;
    setOffset(Math.max(0, touch.clientY - drag.current.startY));
  };
  const onTouchEnd = () => {
    const ended = drag.current;
    drag.current = null;
    setOffset(0);
    if (ended && shouldCloseOnRelease(ended.y - ended.startY, performance.now() - ended.startMs))
      onClose();
  };
  const onTouchCancel = () => {
    drag.current = null;
    setOffset(0);
  };

  return { offset, handlers: { onTouchStart, onTouchMove, onTouchEnd, onTouchCancel } };
}
