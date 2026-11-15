import { act, createElement } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, test, vi } from 'vitest';
import { groupRow, itemRow, noParentRow } from '../../ct/fixtures';
import { useTouchReparent } from './useTouchReparent';

const epic = groupRow({ key: 'SHIP-90', depth: 0, title: 'Storefront' });
const one = groupRow({ key: 'SHIP-101', depth: 1, title: 'Checkout' });
const two = groupRow({ key: 'SHIP-102', depth: 1, title: 'Mobile' });
const story = itemRow({
  key: 'SHIP-141',
  depth: 2,
  parent: { id: one.id, key: one.key, type: one.type!, title: one.title },
});
const none = noParentRow();
const rows = [epic, one, story, two, none];

type Reparent = ReturnType<typeof useTouchReparent>;

// The hook mounted in a bare component; `result.current` is its latest value.
function renderHook(use: () => Reparent) {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const result = { current: undefined as unknown as Reparent };
  function Probe() {
    result.current = use();
    return null;
  }
  const root = createRoot(document.createElement('div'));
  act(() => root.render(createElement(Probe)));
  return { result };
}

describe('useTouchReparent', () => {
  test('useTouchReparent_WhenDroppedOnOwnFeature_ShouldSendNothing', () => {
    const onDrop = vi.fn();
    const { result } = renderHook(() => useTouchReparent(rows, onDrop));

    act(() => result.current.drop(story.id, one.id));
    act(() => result.current.drop(story.id, story.id));
    act(() => result.current.drop(story.id, null));

    expect(onDrop).not.toHaveBeenCalled();
  });

  test('useTouchReparent_WhenDroppedOnAnotherFeatureHeader_ShouldCallOnDropWithThatGroup', () => {
    const onDrop = vi.fn();
    const { result } = renderHook(() => useTouchReparent(rows, onDrop));

    act(() => result.current.drop(story.id, two.id));

    expect(onDrop).toHaveBeenCalledTimes(1);
    expect(onDrop).toHaveBeenCalledWith(story, two);
  });

  test('useTouchReparent_WhenDroppedOnNoParentGroup_ShouldCallOnDropWithIt', () => {
    const onDrop = vi.fn();
    const { result } = renderHook(() => useTouchReparent(rows, onDrop));

    act(() => result.current.drop(story.id, none.id));

    expect(onDrop).toHaveBeenCalledWith(story, none);
  });

  test('useTouchReparent_WhenOverAGroup_ShouldLightOnlyGroups', () => {
    const { result } = renderHook(() => useTouchReparent(rows, vi.fn()));

    act(() => result.current.over(two.id));
    expect(result.current.overId).toBe(two.id);
    act(() => result.current.over(story.id));
    expect(result.current.overId).toBeNull();
    act(() => result.current.over(one.id));
    act(() => result.current.over(null));
    expect(result.current.overId).toBeNull();
  });
});
