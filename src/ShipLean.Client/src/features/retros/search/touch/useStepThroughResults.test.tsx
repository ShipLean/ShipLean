import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom';
import { afterEach, expect, test } from 'vitest';
import type { RetroSearchDto } from '@shiplean/core/api/retros';
import { useStepThroughResults, type StepThroughResults } from './useStepThroughResults';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const row = (id: string, retroId: string) => ({ id, retroId });
const search = {
  rows: [row('p1', 'r1'), row('p2', 'r2'), row('p3', 'r3')],
  selected: { id: 'r1' },
} as unknown as RetroSearchDto;

const seen: { steps?: StepThroughResults; url: string; back?: () => void } = { url: '' };
let root: Root | undefined;

function Probe({ result, point }: { result: RetroSearchDto; point: string }) {
  const steps = useStepThroughResults(result, point);
  const location = useLocation();
  const navigate = useNavigate();
  const url = location.pathname + location.search;
  useEffect(() => {
    seen.steps = steps;
    seen.url = url;
    seen.back = () => navigate(-1);
  });
  return null;
}

function mount(entry: string | string[], result = search, point = 'p1') {
  const host = document.createElement('div');
  root = createRoot(host);
  act(() => {
    root!.render(
      <MemoryRouter initialEntries={Array.isArray(entry) ? entry : [entry]}>
        <Probe result={result} point={point} />
      </MemoryRouter>,
    );
  });
}

afterEach(() => {
  act(() => root?.unmount());
  root = undefined;
});

test('steps to the next result and replaces the entry', () => {
  mount(['/retros?q=x', '/retros?q=x&selected=r1&point=p1']);
  expect(seen.steps!.position).toBe(1);
  expect(seen.steps!.total).toBe(3);
  act(() => seen.steps!.next());
  expect(seen.url).toBe('/retros?q=x&selected=r2&point=p2');
  // Replaced, not pushed: one step back leaves the open retro.
  act(() => seen.back!());
  expect(seen.url).toBe('/retros?q=x');
});

test('disables at the ends', () => {
  mount('/retros?selected=r1&point=p1');
  expect(seen.steps!.canPrevious).toBe(false);
  expect(seen.steps!.canNext).toBe(true);
  const last = { ...search, selected: { id: 'r3' } } as unknown as RetroSearchDto;
  act(() => root?.unmount());
  mount('/retros?selected=r3&point=p3', last, 'p3');
  expect(seen.steps!.canPrevious).toBe(true);
  expect(seen.steps!.canNext).toBe(false);
});

test('keeps the other parameters', () => {
  mount('/retros?q=x&sprint=s1&decision=undecided&selected=r1&point=p1');
  act(() => seen.steps!.next());
  expect(seen.url).toBe('/retros?q=x&sprint=s1&decision=undecided&selected=r2&point=p2');
});
