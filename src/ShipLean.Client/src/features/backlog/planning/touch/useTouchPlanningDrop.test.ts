import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { planningView } from '../../ct/fixtures';
import { sectionDropId, type PlanningDrop } from '../planningTarget';
import { useTouchPlanningDrop, type TouchPlanningDrop } from './useTouchPlanningDrop';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

// Sprint 24: SHIP-160 (13 pts, 26 of 23). Sprint 25: SHIP-158 (8 of 22). Sprint 26: empty. Backlog: SHIP-170 (5 pts), 171, 172.
const view = planningView();
const rowId = (key: string) => `id-${key}`;
const seen: { drop: TouchPlanningDrop | null } = { drop: null };
const onMove = vi.fn<(row: BacklogRowDto, drop: PlanningDrop) => void>();

function Probe() {
  const drop = useTouchPlanningDrop({
    sections: view.sections,
    velocity: view.velocity,
    onMove,
  });
  useEffect(() => {
    seen.drop = drop;
  });
  return null;
}

let root: Root;
beforeEach(async () => {
  onMove.mockClear();
  seen.drop = null;
  root = createRoot(document.createElement('div'));
  await act(async () => root.render(createElement(Probe)));
});
afterEach(() => act(async () => root.unmount()));

test('useTouchPlanningDrop_WhenOverSprintHeader_ShouldGiveResultingSum', async () => {
  await act(async () => seen.drop?.over(rowId('SHIP-170'), sectionDropId('sp25')));

  expect(seen.drop?.lit?.sectionId).toBe('sp25');
  expect(seen.drop?.lit?.sum?.text).toBe('8 + 5 = 13 / 22');
  expect(seen.drop?.lit?.message).toBe('Let go to move SHIP-170 into Sprint 25');
});

test('useTouchPlanningDrop_WhenOverOwnSprintHeader_ShouldLightNothing', async () => {
  await act(async () => seen.drop?.over(rowId('SHIP-158'), sectionDropId('sp25')));

  expect(seen.drop?.lit).toBeNull();
});

test('useTouchPlanningDrop_WhenDroppedOnSprintHeader_ShouldSendASprintMoveWithNoReorder', async () => {
  await act(async () => seen.drop?.drop(rowId('SHIP-170'), sectionDropId('sp25')));

  expect(onMove).toHaveBeenCalledTimes(1);
  const [row, drop] = onMove.mock.calls[0];
  expect(row.key).toBe('SHIP-170');
  expect(drop).toMatchObject({
    sectionId: 'sp25',
    sprintChanged: true,
    beforeItemId: undefined,
    sprint: { id: 'sp25' },
  });
});

test('useTouchPlanningDrop_WhenDroppedOnOwnSprint_ShouldSendNothing', async () => {
  await act(async () => seen.drop?.drop(rowId('SHIP-158'), sectionDropId('sp25')));
  await act(async () => seen.drop?.drop(rowId('SHIP-158'), null));

  expect(onMove).not.toHaveBeenCalled();
});

test('useTouchPlanningDrop_WhenDroppedOnARowOfAnotherSprint_ShouldPutItJustAboveThatRow', async () => {
  await act(async () => seen.drop?.drop(rowId('SHIP-170'), rowId('SHIP-158')));

  expect(onMove.mock.calls[0][1]).toMatchObject({
    sectionId: 'sp25',
    sprintChanged: true,
    beforeItemId: rowId('SHIP-158'),
  });
});

test('useTouchPlanningDrop_WhenDropped_ShouldStopLightingTheHeader', async () => {
  await act(async () => seen.drop?.over(rowId('SHIP-170'), sectionDropId('sp25')));
  await act(async () => seen.drop?.drop(rowId('SHIP-170'), sectionDropId('sp25')));

  expect(seen.drop?.lit).toBeNull();
});
