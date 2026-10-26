import { expect, it } from 'vitest';
import type { BacklogRowDto } from '../../../api/views';
import { backlogView, itemRow } from '../ct/fixtures';
import { moveRow } from './useReorder';

const group = { ...itemRow({ key: 'SHIP-90' }), kind: 'group' } as BacklogRowDto;
const ids = (rows: BacklogRowDto[]) => rows.map((row) => row.key);

it('moveRow_WhenDroppedAboveAnotherItem_ShouldMoveOnlyThatRow', () => {
  const view = backlogView([
    group,
    itemRow({ key: 'SHIP-1' }),
    itemRow({ key: 'SHIP-2' }),
    itemRow({ key: 'SHIP-3' }),
  ]);
  const moved = moveRow(view, 'id-SHIP-3', 'id-SHIP-1');
  expect(ids(moved.rows)).toEqual(['SHIP-90', 'SHIP-3', 'SHIP-1', 'SHIP-2']);
});

it('moveRow_WhenBeforeIsNull_ShouldPutTheRowLast', () => {
  const view = backlogView([itemRow({ key: 'SHIP-1' }), itemRow({ key: 'SHIP-2' }), group]);
  expect(ids(moveRow(view, 'id-SHIP-1', null).rows)).toEqual(['SHIP-2', 'SHIP-90', 'SHIP-1']);
});

it('moveRow_WhenTheRowIsNotThere_ShouldLeaveTheViewAlone', () => {
  const view = backlogView([itemRow({ key: 'SHIP-1' })]);
  expect(moveRow(view, 'nope', null)).toBe(view);
});
