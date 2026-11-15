import { describe, expect, test } from 'vitest';
import { groupRow, itemRow, noParentRow } from '../../ct/fixtures';
import { treePath } from './treePath';

describe('treePath', () => {
  test('treePath_WhenFeatureUnderEpic_ShouldJoinWithArrow', () => {
    const epic = groupRow({ key: 'SHIP-90', depth: 0, title: 'Checkout' });
    const sibling = groupRow({ key: 'SHIP-91', depth: 1, title: 'Cart' });
    const feature = groupRow({ key: 'SHIP-101', depth: 1, title: 'Address autocomplete' });
    const rows = [epic, sibling, itemRow({ key: 'SHIP-5', depth: 2 }), feature];

    expect(treePath(rows, feature)).toBe('Checkout › Address autocomplete');
  });

  test('treePath_WhenNestedFeature_ShouldNameEveryAncestor', () => {
    const epic = groupRow({ key: 'SHIP-90', depth: 0, title: 'Storefront' });
    const feature = groupRow({ key: 'SHIP-101', depth: 1, title: 'Checkout v2' });
    const nested = groupRow({ key: 'SHIP-110', depth: 2, title: 'Receipts' });

    expect(treePath([epic, feature, nested], nested)).toBe('Storefront › Checkout v2 › Receipts');
  });

  test('treePath_WhenNoParentGroup_ShouldBeItsTitle', () => {
    const epic = groupRow({ key: 'SHIP-90', depth: 0, title: 'Storefront' });
    const none = noParentRow({ title: 'No parent' });

    expect(treePath([epic, none], none)).toBe('No parent');
  });
});
