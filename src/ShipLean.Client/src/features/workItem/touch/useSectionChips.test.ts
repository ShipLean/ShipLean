import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { useSectionChips, type SectionTarget } from './useSectionChips';

// The hook reads where each section's root is; jsdom lays nothing out, so each section reports the top a test gives it.
const SECTIONS: SectionTarget[] = [
  { id: 'description', testId: 'section-description' },
  { id: 'tasks', testId: 'section-tasks' },
  { id: 'discussion', testId: 'section-discussion' },
];

let root: Root | undefined;
let host: HTMLDivElement;
const tops = new Map<string, number>();

// Writes the highlighted section where the test can read it.
function Probe() {
  return createElement('output', null, useSectionChips(SECTIONS, true).current ?? 'none');
}

let shown: HTMLDivElement;
const current = () => shown.textContent;

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  host = document.createElement('div');
  for (const section of SECTIONS) {
    const element = document.createElement('section');
    element.dataset.testid = section.testId;
    element.getBoundingClientRect = () => ({ top: tops.get(section.id) ?? 0 }) as DOMRect;
    host.append(element);
  }
  document.body.append(host);
  Object.defineProperty(window, 'innerHeight', { value: 800, configurable: true });
});

afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
  host.remove();
  tops.clear();
});

test('useSectionChips_WhenSectionScrolledIntoView_ShouldHighlightItsChip', () => {
  tops.set('description', 100).set('tasks', 900).set('discussion', 1500);
  shown = document.createElement('div');
  root = createRoot(shown);
  act(() => root!.render(createElement(Probe)));
  expect(current()).toBe('description');

  // Scrolled down: Tasks has passed the reading line (a quarter of the way down), Discussion hasn't.
  tops.set('description', -700).set('tasks', 150).set('discussion', 700);
  act(() => {
    window.dispatchEvent(new Event('scroll'));
  });

  expect(current()).toBe('tasks');
});
