import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { useTouchShell, type TouchShell } from './useTouchShell';
import { PHONE_QUERY, TOUCH_QUERY } from './viewport';

// jsdom has no matchMedia: a fake one per query, whose matches the test sets and whose change listeners it fires.
type FakeQuery = { matches: boolean; listeners: Set<() => void> };
let queries: Map<string, FakeQuery>;
let root: Root | undefined;
let host: HTMLDivElement;

function query(media: string): FakeQuery {
  let found = queries.get(media);
  if (!found) {
    found = { matches: false, listeners: new Set() };
    queries.set(media, found);
  }
  return found;
}

function setMatches(media: string, matches: boolean) {
  const target = query(media);
  target.matches = matches;
  act(() => {
    for (const listener of target.listeners) listener();
  });
}

function Probe() {
  const { touch, viewport } = useTouchShell();
  return <span data-touch={String(touch)} data-viewport={viewport} />;
}

function mount() {
  host = document.createElement('div');
  root = createRoot(host);
  act(() => root?.render(<Probe />));
}

function seen(): TouchShell {
  const probe = host.querySelector('span');
  return {
    touch: probe?.dataset.touch === 'true',
    viewport: probe?.dataset.viewport as TouchShell['viewport'],
  };
}

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  queries = new Map();
  vi.stubGlobal('matchMedia', (media: string) => {
    const target = query(media);
    return {
      media,
      get matches() {
        return target.matches;
      },
      addEventListener: (_type: string, listener: () => void) => target.listeners.add(listener),
      removeEventListener: (_type: string, listener: () => void) =>
        target.listeners.delete(listener),
    };
  });
});

afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
  vi.unstubAllGlobals();
});

test('useTouchShell_WhenTheTouchQueryStartsMatching_ShouldUpdate', () => {
  mount();
  expect(seen()).toEqual({ touch: false, viewport: 'desktop' });

  setMatches(TOUCH_QUERY, true);

  expect(seen()).toEqual({ touch: true, viewport: 'tablet' });
});

test('useTouchShell_WhenThePhoneQueryMatches_ShouldSayPhone', () => {
  query(TOUCH_QUERY).matches = true;
  mount();
  expect(seen()).toEqual({ touch: true, viewport: 'tablet' });

  setMatches(PHONE_QUERY, true);

  expect(seen()).toEqual({ touch: true, viewport: 'phone' });
});
