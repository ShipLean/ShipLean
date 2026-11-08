import { expect, test } from 'vitest';
import { backlogView, itemRow } from '../ct/fixtures';
import { placeContext, placeRequest } from './pastePlace';

test('placeRequest_WhenListIsSortedByAColumn_ShouldPutRowsBeforeTheNextRowInRank', () => {
  const rows = [
    itemRow({ key: 'SHIP-1', rank: 1 }),
    itemRow({ key: 'SHIP-2', rank: 2 }),
    itemRow({ key: 'SHIP-3', rank: 3 }),
  ];
  // Shown sorted by a column: SHIP-1 is focused and SHIP-3 is shown right under it.
  const context = placeContext(backlogView(rows), 'id-SHIP-1', [
    'id-SHIP-1',
    'id-SHIP-3',
    'id-SHIP-2',
  ]);

  expect(placeRequest('below', context).placement).toEqual({
    slot: 'before',
    beforeItemId: 'id-SHIP-2',
  });
});
