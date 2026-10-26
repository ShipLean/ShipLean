import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { useItemPeek, type ItemPeek } from './useItemPeek';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// The latest values the probe saw, written after each render.
const seen = {} as { peek: ItemPeek; address: string; back: () => void };

function Probe() {
  const itemPeek = useItemPeek();
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  useEffect(() => {
    seen.peek = itemPeek;
    seen.address = pathname + search;
    seen.back = () => void navigate(-1);
  });
  return null;
}

let root: Root;
const mountAt = (entry: string) => {
  act(() => {
    root.render(
      <MemoryRouter initialEntries={[entry]}>
        <Probe />
      </MemoryRouter>,
    );
  });
};

beforeEach(() => {
  root = createRoot(document.createElement('div'));
});
afterEach(() => act(() => root.unmount()));

test('useItemPeek_WhenClosedAfterASwap_ShouldReturnToTheBarePage', () => {
  mountAt('/p/SHIP/sprints');
  act(() => seen.peek.open('SHIP-1'));
  act(() => seen.peek.open('SHIP-2'));
  expect(seen.address).toBe('/p/SHIP/sprints?item=SHIP-2');

  act(() => seen.peek.close());

  expect(seen.address).toBe('/p/SHIP/sprints');
});

test('useItemPeek_WhenBackAfterASwap_ShouldShowThePreviousItem', () => {
  mountAt('/p/SHIP/sprints');
  act(() => seen.peek.open('SHIP-1'));
  act(() => seen.peek.open('SHIP-2'));

  act(() => seen.back());

  expect(seen.address).toBe('/p/SHIP/sprints?item=SHIP-1');
});

test('useItemPeek_WhenClosedAfterASwapFromASharedLink_ShouldDropItemFromTheAddress', () => {
  mountAt('/p/SHIP/sprints?item=SHIP-1');
  act(() => seen.peek.open('SHIP-2'));
  expect(seen.address).toBe('/p/SHIP/sprints?item=SHIP-2');

  act(() => seen.peek.close());

  expect(seen.address).toBe('/p/SHIP/sprints');
});

test('useItemPeek_WhenClosedOnASharedLink_ShouldReplaceTheAddressWithoutItem', () => {
  mountAt('/p/SHIP/sprints?item=SHIP-1&x=1');

  act(() => seen.peek.close());

  expect(seen.address).toBe('/p/SHIP/sprints?x=1');
});
