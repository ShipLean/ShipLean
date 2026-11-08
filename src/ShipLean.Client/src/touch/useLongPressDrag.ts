import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react';
import {
  LIFT_MS,
  idleState,
  scrollSpeed,
  step,
  type Area,
  type EdgeDirection,
  type LongPressEvent,
  type LongPressState,
  type Point,
} from './longPress';

// Touch drag after a long press (K6). Touch Events, not pointer events: a pointer stream is cancelled the moment the
// browser starts scrolling. Before the lift the hook leaves every touch alone, so a swipe that starts on the item still
// scrolls the page; after the lift it calls preventDefault() on each touchmove so the page stays put. The lane decides
// what a drop means: this hook only finds the `[data-drop-target]` under the finger.

export interface LongPressDragOptions {
  /** The item lifted (450ms held still). */
  onLift?: (point: Point) => void;
  /** The finger moved while lifted, or the page scrolled under it. `target` is the drop target under the finger. */
  onMove?: (target: HTMLElement | null, point: Point) => void;
  /** Let go after moving. */
  onDrop: (target: HTMLElement | null, point: Point) => void;
  /** Let go after the lift without moving (opens the item's actions). */
  onTapWithoutMove?: () => void;
  /** Held at the left or right edge: after 500ms, then every 800ms (turn the page). */
  onEdge?: (dir: EdgeDirection) => void;
  /** Only the element the props go on starts a drag, with `touch-action: none` on it alone (a row handle). */
  handle?: boolean;
}

export interface LongPressDragProps {
  ref: (element: HTMLElement | null) => () => void;
  style: CSSProperties;
}

export interface LongPressDrag {
  /** Spread on the item, or on the handle with `handle: true`. */
  props: LongPressDragProps;
  lifted: boolean;
  /** Where the finger is, in viewport pixels, for the ghost. */
  x: number;
  y: number;
}

// The visible area at this moment, with the top bar's bottom edge as `top` (--mobile-bar-height plus the safe area).
function currentArea(): Area {
  const probe = document.createElement('div');
  probe.style.cssText =
    'position:fixed;visibility:hidden;height:calc(var(--mobile-bar-height, 0px) + env(safe-area-inset-top, 0px))';
  document.body.appendChild(probe);
  const top = probe.getBoundingClientRect().height;
  probe.remove();
  return { width: window.innerWidth, height: window.innerHeight, top };
}

function dropTargetAt(point: Point): HTMLElement | null {
  return document.elementFromPoint(point.x, point.y)?.closest<HTMLElement>('[data-drop-target]') ?? null;
}

function touchPoint(event: TouchEvent): Point | null {
  const touch = event.changedTouches[0];
  return touch ? { x: touch.clientX, y: touch.clientY } : null;
}

// Card variant: the item still scrolls the page before the lift; no long-press callout or text selection.
const ITEM_STYLE: CSSProperties = { WebkitTouchCallout: 'none', userSelect: 'none', WebkitUserSelect: 'none' };
const HANDLE_STYLE: CSSProperties = { ...ITEM_STYLE, touchAction: 'none' };

interface DragView {
  lifted: boolean;
  x: number;
  y: number;
}

// The timers and listeners behind one hook instance. Callbacks are read through `options()` at the moment they fire,
// so the lane can pass fresh closures on every render.
function createDragEngine(options: () => LongPressDragOptions, setView: (view: DragView) => void) {
  let machine: LongPressState = idleState;
  let liftTimer: number | undefined;
  let frameId: number | undefined;

  function stopTimers() {
    window.clearTimeout(liftTimer);
    if (frameId !== undefined) window.cancelAnimationFrame(frameId);
    liftTimer = undefined;
    frameId = undefined;
  }

  // While lifted: one frame loop keeps the edge clock running and scrolls the page when the finger is near the top or
  // bottom, telling the lane about the target that scrolled under the finger.
  function frame() {
    dispatch({ type: 'tick', at: performance.now() });
    if (machine.phase !== 'lifted') return;
    const speed = scrollSpeed(machine);
    if (speed !== 0) {
      const before = window.scrollY;
      window.scrollBy(0, speed);
      if (window.scrollY !== before) options().onMove?.(dropTargetAt(machine.point), machine.point);
    }
    frameId = window.requestAnimationFrame(frame);
  }

  function dispatch(event: LongPressEvent) {
    const before = machine.phase;
    const { state, effects } = step(machine, event);
    machine = state;
    const callbacks = options();
    for (const effect of effects) {
      switch (effect.type) {
        case 'lift':
          navigator.vibrate?.(15);
          callbacks.onLift?.(effect.point);
          break;
        case 'move':
          callbacks.onMove?.(dropTargetAt(effect.point), effect.point);
          break;
        case 'edge':
          callbacks.onEdge?.(effect.dir);
          break;
        case 'drop':
          callbacks.onDrop(dropTargetAt(effect.point), effect.point);
          break;
        case 'tap':
          callbacks.onTapWithoutMove?.();
          break;
      }
    }
    const lifted = state.phase === 'lifted';
    if (lifted || before === 'lifted') setView({ lifted, x: state.point.x, y: state.point.y });
    if (lifted && before !== 'lifted') frameId = window.requestAnimationFrame(frame);
    if (!lifted && state.phase !== 'pressing') stopTimers();
  }

  function attach(element: HTMLElement): () => void {
    const onStart = (event: TouchEvent) => {
      const point = touchPoint(event);
      if (event.touches.length !== 1 || point === null) return;
      stopTimers();
      dispatch({ type: 'press', point, at: performance.now(), area: currentArea() });
      liftTimer = window.setTimeout(() => dispatch({ type: 'tick', at: performance.now() }), LIFT_MS);
    };
    const onMove = (event: TouchEvent) => {
      const point = touchPoint(event);
      if (point === null) return;
      dispatch({ type: 'move', point, at: performance.now() });
      if (machine.phase === 'lifted' && event.cancelable) event.preventDefault();
    };
    const onEnd = (event: TouchEvent) => {
      const wasLifted = machine.phase === 'lifted';
      dispatch({ type: 'release', at: performance.now() });
      // No click or mouse events after a drop or a tap-after-lift: the lane has handled it.
      if (wasLifted && event.cancelable) event.preventDefault();
    };
    const onCancel = () => dispatch({ type: 'cancel' });
    const onContextMenu = (event: Event) => {
      if (machine.phase === 'pressing' || machine.phase === 'lifted') event.preventDefault();
    };
    element.addEventListener('touchstart', onStart, { passive: true });
    element.addEventListener('touchmove', onMove, { passive: false });
    element.addEventListener('touchend', onEnd, { passive: false });
    element.addEventListener('touchcancel', onCancel);
    element.addEventListener('contextmenu', onContextMenu);
    return () => {
      element.removeEventListener('touchstart', onStart);
      element.removeEventListener('touchmove', onMove);
      element.removeEventListener('touchend', onEnd);
      element.removeEventListener('touchcancel', onCancel);
      element.removeEventListener('contextmenu', onContextMenu);
      dispatch({ type: 'cancel' });
    };
  }

  return { attach, stop: () => dispatch({ type: 'cancel' }) };
}

export function useLongPressDrag(options: LongPressDragOptions): LongPressDrag {
  const optionsRef = useRef(options);
  useEffect(() => {
    optionsRef.current = options;
  });
  const [view, setView] = useState<DragView>({ lifted: false, x: 0, y: 0 });
  const [engine] = useState(() => createDragEngine(() => optionsRef.current, setView));
  useEffect(() => engine.stop, [engine]);
  const ref = useCallback((element: HTMLElement | null) => (element ? engine.attach(element) : () => {}), [engine]);

  return { props: { ref, style: options.handle ? HANDLE_STYLE : ITEM_STYLE }, ...view };
}
