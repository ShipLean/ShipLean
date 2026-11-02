import { act, useEffect, type ReactNode } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { SelectionProvider } from './SelectionProvider';
import { useSelection, type Selection } from './useSelection';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const order = ['a', 'b', 'c', 'd'];
const seen: { owner: Selection | null; cell: Selection | null } = { owner: null, cell: null };

function Owner() {
  const selection = useSelection(order);
  useEffect(() => {
    seen.owner = selection;
  });
  return null;
}

function Cell() {
  const selection = useSelection();
  useEffect(() => {
    seen.cell = selection;
  });
  return null;
}

let root: Root;
const mount = (children: ReactNode) => act(() => root.render(children));

beforeEach(() => {
  seen.owner = null;
  seen.cell = null;
  root = createRoot(document.createElement('div'));
});
afterEach(() => act(() => root.unmount()));

test('a no-argument consumer ranges and selects all over the owners order', () => {
  mount(
    <SelectionProvider>
      <Owner />
      <Cell />
    </SelectionProvider>,
  );

  act(() => seen.cell?.toggle('b'));
  act(() => seen.cell?.rangeTo('d'));
  expect([...(seen.cell?.ids ?? [])]).toEqual(['b', 'c', 'd']);

  act(() => seen.cell?.selectAll());
  expect(seen.cell?.count).toBe(4);
  expect(seen.cell?.orderedIds).toEqual(order);
});

test('useSelection outside a provider is inert', () => {
  mount(<Cell />);

  const selection = seen.cell;
  act(() => {
    selection?.toggle('a');
    selection?.rangeTo('b');
    selection?.selectAll();
    selection?.setFocused('a');
    selection?.clear();
  });

  expect(selection?.count).toBe(0);
  expect(selection?.has('a')).toBe(false);
  expect(selection?.orderedIds).toEqual([]);
  expect(selection?.focusedId).toBeNull();
  expect(seen.cell).toBe(selection);
});
