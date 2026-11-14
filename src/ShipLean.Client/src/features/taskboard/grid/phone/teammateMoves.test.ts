import { MutationObserver, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, createElement } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it } from 'vitest';
import type { CardDto, TaskboardPersonRowDto } from '@shiplean/core/api/views';
import { taskboardMoveKey } from '../../taskboardApi';
import type { TaskboardGridView } from '../../taskboardGridView';
import type { TaskboardScope } from '../../taskboardScope';
import { landedStates, useTeammatePulses } from './teammateMoves';

const card = (id: string) => ({ id, key: id }) as unknown as CardDto;
const viewOf = (placed: Record<string, string[]>): TaskboardGridView => {
  const row: TaskboardPersonRowDto = {
    person: null,
    taskCount: 0,
    cells: Object.entries(placed).map(([stateId, ids]) => ({ stateId, tasks: ids.map(card) })),
  };
  return { peopleRows: [row] } as unknown as TaskboardGridView;
};

describe('landedStates', () => {
  it('LandedStates_WhenTaskChangedState_ShouldNameTheStateItLandedIn', () => {
    const before = viewOf({ new: ['a', 'b'], active: [] });
    const after = viewOf({ new: ['a'], active: ['b'] });

    expect(landedStates(before, after)).toEqual(['active']);
  });

  it('LandedStates_WhenTasksOnlyAppearOrVanish_ShouldNameNothing', () => {
    const before = viewOf({ new: ['a'], active: ['c'] });
    const after = viewOf({ new: ['a', 'created'], active: [] });

    expect(landedStates(before, after)).toEqual([]);
  });

  it('LandedStates_WhenTwoTasksLandInOneState_ShouldNameItOnce', () => {
    const before = viewOf({ new: ['a', 'b'], active: [] });
    const after = viewOf({ new: [], active: ['a', 'b'] });

    expect(landedStates(before, after)).toEqual(['active']);
  });
});

describe('useTeammatePulses', () => {
  const scope: TaskboardScope = { kind: 'sprint', sprintId: 's1' };
  const START = viewOf({ new: ['a'], active: [] });
  const MOVED = viewOf({ new: [], active: ['a'] });

  function setup() {
    Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
    const client = new QueryClient();
    const seen = { pulses: new Map<string, number>() };
    function Probe({ view }: { view: TaskboardGridView }) {
      seen.pulses = new Map(useTeammatePulses('p1', scope, view));
      return null;
    }
    const root = createRoot(document.createElement('div'));
    const show = (view: TaskboardGridView) =>
      act(() => {
        root.render(createElement(QueryClientProvider, { client }, createElement(Probe, { view })));
      });
    show(START);
    return { client, seen, show };
  }

  it('UseTeammatePulses_WhenViewChangesWithNoMovePending_ShouldCountThePulseOfTheLandedState', () => {
    const { seen, show } = setup();
    expect(seen.pulses.size).toBe(0);

    show(MOVED);

    expect(seen.pulses.get('active')).toBe(1);
    expect(seen.pulses.has('new')).toBe(false);
  });

  it('UseTeammatePulses_WhenOwnMoveIsPending_ShouldNotPulse', () => {
    const { client, seen, show } = setup();
    const own = new MutationObserver(client, {
      mutationKey: taskboardMoveKey('p1', scope),
      mutationFn: () => new Promise<void>(() => undefined),
    });
    act(() => {
      void own.mutate();
    });

    show(MOVED);

    expect(seen.pulses.size).toBe(0);
  });

  it('UseTeammatePulses_WhenSecondTeammateMoveLandsInSameState_ShouldCountAgain', () => {
    const { seen, show } = setup();
    show(MOVED);

    show(viewOf({ new: [], active: [], closed: ['a'] }));
    show(MOVED);

    expect(seen.pulses.get('active')).toBe(2);
    expect(seen.pulses.get('closed')).toBe(1);
  });
});
