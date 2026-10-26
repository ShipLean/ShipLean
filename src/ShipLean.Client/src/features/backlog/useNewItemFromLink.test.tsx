import { act, StrictMode, useEffect, useSyncExternalStore } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, expect, test } from 'vitest';
import type { NewItemKind } from '../../routes';
import { useNewItemFromLink } from './useNewItemFromLink';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const ACCEPTS: readonly NewItemKind[] = ['story', 'bug'];
const seen = { address: '', calls: [] as NewItemKind[] };
const onKind = (kind: NewItemKind) => void seen.calls.push(kind);

function Probe() {
  useNewItemFromLink(ACCEPTS, onKind);
  const { pathname, search } = useLocation();
  useEffect(() => {
    seen.address = pathname + search;
  });
  return null;
}

let root: Root;
const mountAt = (entry: string) =>
  act(() => {
    root.render(
      <MemoryRouter initialEntries={[entry]}>
        <Probe />
      </MemoryRouter>,
    );
  });

beforeEach(() => {
  seen.calls = [];
  root = createRoot(document.createElement('div'));
});
afterEach(() => act(() => root.unmount()));

test('useNewItemFromLink_WhenKindAccepted_ShouldCallOnKindOnceAndDropTheQuery', () => {
  mountAt('/p/SHIP/backlogs/stories?new=bug');

  expect(seen.calls).toEqual(['bug']);
  expect(seen.address).toBe('/p/SHIP/backlogs/stories');
});

test('useNewItemFromLink_WhenKindNotAccepted_ShouldNotCallAndDropTheQuery', () => {
  mountAt('/p/SHIP/backlogs/stories?new=epic');
  expect(seen.calls).toEqual([]);
  expect(seen.address).toBe('/p/SHIP/backlogs/stories');

  act(() => root.unmount());
  root = createRoot(document.createElement('div'));
  mountAt('/p/SHIP/backlogs/stories?new=task');
  expect(seen.calls).toEqual([]);
  expect(seen.address).toBe('/p/SHIP/backlogs/stories');
});

test('useNewItemFromLink_WhenOtherQueries_ShouldKeepThem', () => {
  mountAt('/p/SHIP/backlogs/stories?mode=tree&new=story&item=SHIP-1');

  expect(seen.calls).toEqual(['story']);
  expect(seen.address).toBe('/p/SHIP/backlogs/stories?mode=tree&item=SHIP-1');
});

test('useNewItemFromLink_WhenRerenderedBeforeTheAddressChanges_ShouldCallOnKindOnce', () => {
  // A store the callback pokes, so the page re-renders (with a fresh inline callback) while `new` is still in the address.
  let version = 0;
  const listeners = new Set<() => void>();
  const bump = () => {
    version += 1;
    listeners.forEach((listener) => listener());
  };

  function Rerendering() {
    useSyncExternalStore(
      (listener) => {
        listeners.add(listener);
        return () => void listeners.delete(listener);
      },
      () => version,
    );
    useNewItemFromLink(ACCEPTS, (kind) => {
      seen.calls.push(kind);
      bump();
    });
    return null;
  }

  act(() => {
    root.render(
      <StrictMode>
        <MemoryRouter initialEntries={['/p/SHIP/backlogs/stories?new=bug']}>
          <Rerendering />
        </MemoryRouter>
      </StrictMode>,
    );
  });

  expect(seen.calls).toEqual(['bug']);
});
