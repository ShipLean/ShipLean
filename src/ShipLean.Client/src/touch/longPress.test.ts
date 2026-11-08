import { expect, test } from 'vitest';
import {
  EDGE_FIRST_MS,
  EDGE_REPEAT_MS,
  LIFT_MS,
  idleState,
  liftDelay,
  scrollSpeed,
  step,
  type Area,
  type LongPressEffect,
  type LongPressEvent,
  type LongPressState,
  type Point,
} from './longPress';

// A 390×844 phone with a 48px top bar. Time is passed in by hand: the machine has no clock of its own.
const AREA: Area = { width: 390, height: 844, top: 48 };
const MIDDLE: Point = { x: 195, y: 400 };

function run(events: LongPressEvent[], from: LongPressState = idleState) {
  let state = from;
  const effects: LongPressEffect[] = [];
  for (const event of events) {
    const next = step(state, event);
    state = next.state;
    effects.push(...next.effects);
  }
  return { state, effects };
}

function lifted(at: Point = MIDDLE) {
  return run([
    { type: 'press', point: at, at: 0, area: AREA },
    { type: 'tick', at: LIFT_MS },
  ]).state;
}

const edges = (effects: LongPressEffect[]) => effects.filter((e) => e.type === 'edge');

test('longPress_WhenHeld450ms_ShouldLift', () => {
  const early = run([
    { type: 'press', point: MIDDLE, at: 0, area: AREA },
    { type: 'tick', at: 449 },
  ]);
  expect(early.state.phase).toBe('pressing');
  expect(early.effects).toEqual([]);

  const held = step(early.state, { type: 'tick', at: 450 });
  expect(held.state.phase).toBe('lifted');
  expect(held.effects).toEqual([{ type: 'lift', point: MIDDLE }]);
});

test('longPress_WhenTheTimerFiresAHairEarly_ShouldWaitTheRestThenLift', () => {
  const early = run([
    { type: 'press', point: MIDDLE, at: 0, area: AREA },
    { type: 'tick', at: 449.6 },
  ]);
  expect(early.state.phase).toBe('pressing');
  expect(liftDelay(early.state, 449.6)).toBeCloseTo(0.4);

  const held = step(early.state, { type: 'tick', at: 450 });
  expect(held.state.phase).toBe('lifted');
  expect(liftDelay(held.state, 450)).toBe(0);
});

test('longPress_WhenMovedBeforeTheLift_ShouldCancel', () => {
  const { state, effects } = run([
    { type: 'press', point: MIDDLE, at: 0, area: AREA },
    { type: 'move', point: { x: MIDDLE.x, y: MIDDLE.y + 4 }, at: 100 },
    { type: 'move', point: { x: MIDDLE.x, y: MIDDLE.y + 9 }, at: 150 },
    { type: 'tick', at: LIFT_MS },
    { type: 'release', at: LIFT_MS + 10 },
  ]);
  expect(state.phase).toBe('cancelled');
  expect(effects).toEqual([]);
});

test('longPress_WhenReleasedAfterLiftWithoutMoving_ShouldBeATap', () => {
  const tap = run(
    [
      { type: 'move', point: { x: MIDDLE.x + 3, y: MIDDLE.y }, at: LIFT_MS + 50 },
      { type: 'release', at: LIFT_MS + 100 },
    ],
    lifted(),
  );
  expect(tap.state.phase).toBe('dropped');
  expect(tap.effects.at(-1)).toEqual({ type: 'tap' });

  const target = { x: MIDDLE.x, y: MIDDLE.y + 120 };
  const drop = run(
    [
      { type: 'move', point: target, at: LIFT_MS + 50 },
      { type: 'release', at: LIFT_MS + 100 },
    ],
    lifted(),
  );
  expect(drop.effects.at(-1)).toEqual({ type: 'drop', point: target });
});

test('longPress_WhenHeldAtTheRightEdge_ShouldFireEdgeAfter500msThenEvery800ms', () => {
  const enteredAt = 1000;
  const edge = { x: AREA.width - 10, y: MIDDLE.y };
  let { state, effects } = run([{ type: 'move', point: edge, at: enteredAt }], lifted());
  expect(edges(effects)).toEqual([]);

  ({ state, effects } = run([{ type: 'tick', at: enteredAt + EDGE_FIRST_MS - 1 }], state));
  expect(edges(effects)).toEqual([]);
  ({ state, effects } = run([{ type: 'tick', at: enteredAt + EDGE_FIRST_MS }], state));
  expect(edges(effects)).toEqual([{ type: 'edge', dir: 'right' }]);

  ({ state, effects } = run(
    [{ type: 'tick', at: enteredAt + EDGE_FIRST_MS + EDGE_REPEAT_MS - 1 }],
    state,
  ));
  expect(edges(effects)).toEqual([]);
  ({ effects } = run([{ type: 'tick', at: enteredAt + EDGE_FIRST_MS + EDGE_REPEAT_MS }], state));
  expect(edges(effects)).toEqual([{ type: 'edge', dir: 'right' }]);
});

test('longPress_WhenLeavingTheEdge_ShouldStopFiring', () => {
  const { state, effects } = run(
    [
      { type: 'move', point: { x: 5, y: MIDDLE.y }, at: 1000 },
      { type: 'tick', at: 1000 + EDGE_FIRST_MS },
      { type: 'move', point: MIDDLE, at: 1600 },
      { type: 'tick', at: 1000 + EDGE_FIRST_MS + EDGE_REPEAT_MS },
      { type: 'tick', at: 5000 },
    ],
    lifted(),
  );
  expect(edges(effects)).toEqual([{ type: 'edge', dir: 'left' }]);
  expect(state.edge).toBeNull();
});

test('longPress_WhenNearTheBottom_ShouldAskToScrollDown', () => {
  const middle = lifted();
  expect(scrollSpeed(middle)).toBe(0);

  const nearBottom = run(
    [{ type: 'move', point: { x: MIDDLE.x, y: AREA.height - 10 }, at: 600 }],
    middle,
  ).state;
  expect(scrollSpeed(nearBottom)).toBeGreaterThan(0);

  const underTheBar = run(
    [{ type: 'move', point: { x: MIDDLE.x, y: AREA.top + 10 }, at: 600 }],
    middle,
  ).state;
  expect(scrollSpeed(underTheBar)).toBeLessThan(0);
});

test('longPress_WhenTheEdgeTickStalls_ShouldFireOnceThenKeepTheBeat', () => {
  const enteredAt = 1000;
  let { state } = run(
    [{ type: 'move', point: { x: AREA.width - 10, y: MIDDLE.y }, at: enteredAt }],
    lifted(),
  );
  const stalledTo = enteredAt + EDGE_FIRST_MS + 3 * EDGE_REPEAT_MS;

  let effects: LongPressEffect[];
  ({ state, effects } = run([{ type: 'tick', at: stalledTo }], state));
  expect(edges(effects)).toEqual([{ type: 'edge', dir: 'right' }]);

  ({ state, effects } = run([{ type: 'tick', at: stalledTo + 799 }], state));
  expect(edges(effects)).toEqual([]);
  ({ effects } = run([{ type: 'tick', at: stalledTo + 800 }], state));
  expect(edges(effects)).toEqual([{ type: 'edge', dir: 'right' }]);
});
