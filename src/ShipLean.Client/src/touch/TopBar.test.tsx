import { act, memo } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { TopBarProvider, useTopBar, useTopBarSlots, type TopBarSlots } from './TopBar';

// The slots as the bar sees them, written out as text so a test can compare them.
let root: Root | undefined;
let host: HTMLDivElement;

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  host = document.createElement('div');
  document.body.append(host);
});

afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
  host.remove();
});

function Bar() {
  const slots = useTopBarSlots();
  return (
    <output data-testid="bar">
      {JSON.stringify({
        title: typeof slots.title === 'string' ? slots.title : undefined,
        back: slots.back?.label,
        action: typeof slots.action === 'string' ? slots.action : undefined,
        pinned: typeof slots.pinned === 'string' ? slots.pinned : undefined,
      })}
    </output>
  );
}

// Memoised, as a real page that isn't re-rendering: a caller that stays must keep its keys without setting them again.
const Caller = memo(function Caller({ slots }: { slots: TopBarSlots }) {
  useTopBar(slots);
  return null;
});

function render(callers: Array<{ id: string; slots: TopBarSlots }>) {
  act(() => {
    root ??= createRoot(host);
    root.render(
      <TopBarProvider>
        {callers.map((caller) => (
          <Caller key={caller.id} slots={caller.slots} />
        ))}
        <Bar />
      </TopBarProvider>,
    );
  });
}

function bar(): Record<string, string | undefined> {
  return JSON.parse(host.querySelector('[data-testid="bar"]')!.textContent!) as Record<
    string,
    string | undefined
  >;
}

test('useTopBar_WhenThePageUnmounts_ShouldClearItsSlots', () => {
  render([{ id: 'page', slots: { title: 'Sprint 24', back: { to: '/x', label: 'Back' } } }]);
  expect(bar()).toEqual({ title: 'Sprint 24', back: 'Back' });

  render([]);

  expect(bar()).toEqual({});
});

test('useTopBar_WhenSlotsChange_ShouldShowTheLatest', () => {
  render([{ id: 'page', slots: { title: 'One', action: 'Add' } }]);

  render([{ id: 'page', slots: { title: 'Two' } }]);

  expect(bar()).toEqual({ title: 'Two' });
});

test('useTopBar_WhenAnotherPageTookOver_ShouldNotClearItsSlots', () => {
  const old = { id: 'old', slots: { title: 'Old page' } };
  const next = { id: 'new', slots: { title: 'New page' } };
  render([old]);
  render([old, next]);
  expect(bar()).toEqual({ title: 'New page' });

  render([next]);

  expect(bar()).toEqual({ title: 'New page' });
});

test('useTopBar_WhenTwoCallersSetDifferentSlots_ShouldKeepBoth', () => {
  const page = { id: 'page', slots: { title: 'SHIP-101', back: { to: '/b', label: 'Backlog' } } };
  render([page]);
  render([page, { id: 'panel', slots: { action: 'Edit' } }]);

  expect(bar()).toEqual({ title: 'SHIP-101', back: 'Backlog', action: 'Edit' });
});

test('useTopBar_WhenPinnedCallerUnmounts_ShouldRestoreTheOtherCallersBack', () => {
  const page = { id: 'page', slots: { back: { to: '/b', label: 'Backlog' }, action: 'Edit' } };
  render([page, { id: 'editor', slots: { pinned: 'Toolbar', action: 'Done' } }]);
  expect(bar()).toEqual({ back: 'Backlog', action: 'Done', pinned: 'Toolbar' });

  render([page]);

  expect(bar()).toEqual({ back: 'Backlog', action: 'Edit' });
});
