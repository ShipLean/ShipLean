// The long-press drag as a pure state machine (K6). Time comes in with every event, so the machine has no timers; the
// hook (useLongPressDrag) owns the timers and turns the effects into callbacks.
//
//   idle → pressing → lifted → dropped | cancelled
//
// Pressing still for LIFT_MS lifts. Moving more than MOVE_TOLERANCE_PX while pressing cancels: the finger was scrolling.
// After the lift, holding in the left or right edge zone fires `edge` after EDGE_FIRST_MS and then every EDGE_REPEAT_MS,
// and a finger near the top or bottom asks for a vertical scroll. Letting go after the lift without moving is a tap.

export const LIFT_MS = 450;
export const MOVE_TOLERANCE_PX = 8;
export const EDGE_ZONE_PX = 32;
export const EDGE_FIRST_MS = 500;
export const EDGE_REPEAT_MS = 800;
export const SCROLL_ZONE_PX = 48;
/** The fastest auto-scroll, in pixels per frame, with the finger at the very edge of the screen. */
export const SCROLL_MAX_PX = 12;

export type Phase = 'idle' | 'pressing' | 'lifted' | 'dropped' | 'cancelled';
export type EdgeDirection = 'left' | 'right';

export interface Point {
  x: number;
  y: number;
}

/** The visible area the finger moves in: the viewport, with the top bar's bottom edge as `top`. */
export interface Area {
  width: number;
  height: number;
  top: number;
}

interface EdgeHold {
  dir: EdgeDirection;
  nextAt: number;
}

export interface LongPressState {
  phase: Phase;
  start: Point;
  startAt: number;
  point: Point;
  area: Area;
  /** True once the finger has gone further than MOVE_TOLERANCE_PX from where it started, after the lift. */
  moved: boolean;
  edge: EdgeHold | null;
}

export type LongPressEvent =
  | { type: 'press'; point: Point; at: number; area: Area }
  | { type: 'move'; point: Point; at: number }
  | { type: 'tick'; at: number }
  | { type: 'release'; at: number }
  | { type: 'cancel' };

export type LongPressEffect =
  | { type: 'lift'; point: Point }
  | { type: 'move'; point: Point }
  | { type: 'edge'; dir: EdgeDirection }
  | { type: 'drop'; point: Point }
  | { type: 'tap' };

export interface Step {
  state: LongPressState;
  effects: LongPressEffect[];
}

export const idleState: LongPressState = {
  phase: 'idle',
  start: { x: 0, y: 0 },
  startAt: 0,
  point: { x: 0, y: 0 },
  area: { width: 0, height: 0, top: 0 },
  moved: false,
  edge: null,
};

function distance(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function edgeAt(point: Point, area: Area): EdgeDirection | null {
  if (point.x < EDGE_ZONE_PX) return 'left';
  if (point.x > area.width - EDGE_ZONE_PX) return 'right';
  return null;
}

// Fires the edge turn that is due by `at`, at most one per event: after a stall (a background tab, a long frame) the
// pager turns one page, not one for every missed beat, and the beat restarts from now.
function advanceEdge(
  state: LongPressState,
  at: number,
  effects: LongPressEffect[],
): LongPressState {
  const dir = edgeAt(state.point, state.area);
  if (dir === null) return state.edge === null ? state : { ...state, edge: null };
  let edge = state.edge?.dir === dir ? state.edge : { dir, nextAt: at + EDGE_FIRST_MS };
  if (at >= edge.nextAt) {
    effects.push({ type: 'edge', dir });
    const onBeat = edge.nextAt + EDGE_REPEAT_MS;
    edge = { dir, nextAt: at < onBeat ? onBeat : at + EDGE_REPEAT_MS };
  }
  return edge === state.edge ? state : { ...state, edge };
}

// Lifts a press that has been held long enough by `at`.
function liftIfDue(state: LongPressState, at: number, effects: LongPressEffect[]): LongPressState {
  if (state.phase !== 'pressing' || at - state.startAt < LIFT_MS) return state;
  effects.push({ type: 'lift', point: state.point });
  return { ...state, phase: 'lifted' };
}

/** How long until a press lifts, in ms: 0 when it is due (or not pressing). The hook re-arms its timer with this. */
export function liftDelay(state: LongPressState, at: number): number {
  if (state.phase !== 'pressing') return 0;
  return Math.max(0, state.startAt + LIFT_MS - at);
}

/** Advances the machine by one event and says what the hook should do about it. */
export function step(state: LongPressState, event: LongPressEvent): Step {
  const effects: LongPressEffect[] = [];
  switch (event.type) {
    case 'press':
      return {
        state: {
          ...idleState,
          phase: 'pressing',
          start: event.point,
          startAt: event.at,
          point: event.point,
          area: event.area,
        },
        effects,
      };
    case 'move': {
      let next = liftIfDue(state, event.at, effects);
      if (next.phase === 'pressing') {
        const scrolled = distance(event.point, next.start) > MOVE_TOLERANCE_PX;
        return {
          state: scrolled ? { ...next, phase: 'cancelled' } : { ...next, point: event.point },
          effects,
        };
      }
      if (next.phase !== 'lifted') return { state: next, effects };
      next = {
        ...next,
        point: event.point,
        moved: next.moved || distance(event.point, next.start) > MOVE_TOLERANCE_PX,
      };
      effects.push({ type: 'move', point: event.point });
      return { state: advanceEdge(next, event.at, effects), effects };
    }
    case 'tick': {
      const next = liftIfDue(state, event.at, effects);
      if (next.phase !== 'lifted') return { state: next, effects };
      return { state: advanceEdge(next, event.at, effects), effects };
    }
    case 'release': {
      const next = liftIfDue(state, event.at, effects);
      if (next.phase !== 'lifted')
        return { state: { ...next, phase: 'cancelled', edge: null }, effects };
      effects.push(next.moved ? { type: 'drop', point: next.point } : { type: 'tap' });
      return { state: { ...next, phase: 'dropped', edge: null }, effects };
    }
    case 'cancel':
      return { state: { ...state, phase: 'cancelled', edge: null }, effects };
  }
}

/**
 * The vertical auto-scroll the finger asks for, in pixels per frame: negative scrolls up, positive down, 0 for none.
 * It speeds up the deeper the finger is in the zone (SCROLL_ZONE_PX below the top bar, or above the bottom).
 */
export function scrollSpeed(state: LongPressState): number {
  if (state.phase !== 'lifted') return 0;
  const { y } = state.point;
  const { top, height } = state.area;
  const intoTop = top + SCROLL_ZONE_PX - y;
  if (intoTop > 0)
    return -Math.ceil((Math.min(intoTop, SCROLL_ZONE_PX) / SCROLL_ZONE_PX) * SCROLL_MAX_PX);
  const intoBottom = y - (height - SCROLL_ZONE_PX);
  if (intoBottom > 0)
    return Math.ceil((Math.min(intoBottom, SCROLL_ZONE_PX) / SCROLL_ZONE_PX) * SCROLL_MAX_PX);
  return 0;
}
