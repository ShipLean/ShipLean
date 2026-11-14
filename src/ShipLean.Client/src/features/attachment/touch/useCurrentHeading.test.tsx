import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { currentAnchor, useCurrentHeading } from './useCurrentHeading';

let root: Root | undefined;
let host: HTMLDivElement;

const seen = () => host.querySelector('span')?.dataset.heading ?? null;

function Probe({ anchors, enabled }: { anchors: string[]; enabled: boolean }) {
  const heading = useCurrentHeading(anchors, enabled);
  return <span data-heading={heading ?? undefined} />;
}

function mountProbe(anchors: string[], enabled: boolean) {
  host = document.body.appendChild(document.createElement('div'));
  root = createRoot(host);
  act(() => root?.render(<Probe anchors={anchors} enabled={enabled} />));
}

function heading(id: string, top: number) {
  const element = document.createElement('h2');
  element.id = id;
  element.getBoundingClientRect = () => ({ top }) as DOMRect;
  document.body.append(element);
  return element;
}

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
});

afterEach(() => {
  act(() => root?.unmount());
  document.body.replaceChildren();
});

test('useCurrentHeading_WhenHeadingPassesTop_ShouldNameIt', () => {
  const intro = heading('intro', 10);
  const tokens = heading('tokens', 400);
  mountProbe(['intro', 'tokens'], true);
  expect(seen()).toBe('intro');

  tokens.getBoundingClientRect = () => ({ top: 60 }) as DOMRect;
  intro.getBoundingClientRect = () => ({ top: -500 }) as DOMRect;
  act(() => {
    document.dispatchEvent(new Event('scroll'));
  });

  expect(seen()).toBe('tokens');
});

test('useCurrentHeading_WhenDisabled_ShouldNameNothing', () => {
  heading('intro', 10);
  mountProbe(['intro'], false);
  expect(seen()).toBeNull();
});

test('currentAnchor_WhenNoHeadingReachedTheLine_ShouldNameTheFirst', () => {
  const tops = [
    { anchor: 'a', top: 300 },
    { anchor: 'b', top: 900 },
  ];
  expect(currentAnchor(tops, 96)).toBe('a');
});

test('useCurrentHeading_WhenNextHeadingIsBelowTheLine_ShouldKeepTheOneAbove', () => {
  heading('intro', -500);
  heading('tokens', 400);
  mountProbe(['intro', 'tokens'], true);

  expect(seen()).toBe('intro');
});
