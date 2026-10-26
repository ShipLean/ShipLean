import { expect, test } from 'vitest';
import { boardRootKey } from '../boardApi';
import { childrenKey } from './childrenApi';

test('childrenKey_WhenBuilt_ShouldStayOutsideTheBoardRootSoMovesNeverRewriteIt', () => {
  const root = boardRootKey('p1', 'b1');
  expect(childrenKey('p1', 'i1', 'story').slice(0, root.length)).not.toEqual(root);
});
