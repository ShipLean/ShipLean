import { describe, expect, it } from 'vitest';
import { treeRows, treeView } from '../ct/fixtures';
import { placeRow, treeTarget, withWritten } from './treeTarget';

const { rows } = treeRows();
const row = (key: string) => rows.find((r) => r.key === key)!;
const noParent = rows.find((r) => r.kind === 'noParentGroup')!;

describe('treeTarget', () => {
  it('makes a Feature row the parent without reordering', () => {
    const drop = treeTarget(rows, row('SHIP-141'), row('SHIP-102'))!;
    expect(drop.parentId).toBe(row('SHIP-102').id);
    expect(drop.beforeItemId).toBeUndefined();
    expect(drop.done).toBe('SHIP-141 moved under Mobile sign-in');
  });

  it('takes the parent of a story in another Feature and goes above it', () => {
    const drop = treeTarget(rows, row('SHIP-141'), row('SHIP-129'))!;
    expect(drop.parentId).toBe(row('SHIP-102').id);
    expect(drop.beforeItemId).toBe(row('SHIP-129').id);
    expect(drop.placement).toEqual({ before: row('SHIP-129').id });
    expect(drop.depth).toBe(2);
    expect(drop.parent?.title).toBe('Mobile sign-in');
  });

  it('only reorders among stories of the same parent', () => {
    const drop = treeTarget(rows, row('SHIP-144'), row('SHIP-141'))!;
    expect(drop.parentId).toBeUndefined();
    expect(drop.beforeItemId).toBe(row('SHIP-141').id);
    expect(drop.done).toBe('SHIP-144 reordered');
  });

  it('clears the parent on the No parent group', () => {
    const drop = treeTarget(rows, row('SHIP-141'), noParent)!;
    expect(drop.parentId).toBeNull();
    expect(drop.beforeItemId).toBeUndefined();
    expect(drop.placement).toEqual({ under: noParent.id });
    expect(drop.parent).toBeNull();
  });

  it('clears the parent of a story under a Removed Feature, whose Parent is not null', () => {
    expect(treeTarget(rows, row('SHIP-160'), noParent)?.parentId).toBeNull();
  });

  it('takes no parent from a story with none', () => {
    expect(treeTarget(rows, row('SHIP-141'), row('SHIP-170'))?.parentId).toBeNull();
  });

  it('does nothing for a drop on itself, on its own parent, or on No parent when it has none', () => {
    expect(treeTarget(rows, row('SHIP-141'), row('SHIP-141'))).toBeNull();
    expect(treeTarget(rows, row('SHIP-141'), row('SHIP-101'))).toBeNull();
    expect(treeTarget(rows, row('SHIP-170'), noParent)).toBeNull();
  });

  it('takes the nearest live group when dropped on a story under a Removed Feature', () => {
    // SHIP-160's Parent is the Removed Feature SHIP-99, but it sits under Epic SHIP-90.
    const drop = treeTarget(rows, row('SHIP-141'), row('SHIP-160'))!;
    expect(drop.parentId).toBe(row('SHIP-90').id);
    expect(drop.parentId).not.toBe(row('SHIP-160').parent!.id);
    expect(drop.beforeItemId).toBe(row('SHIP-160').id);
    expect(drop.done).toBe('SHIP-141 moved under Storefront');
  });

  it('places a group drop first under the group, at the group depth plus one, with or without stories', () => {
    for (const [dragged, group] of [
      ['SHIP-129', 'SHIP-101'],
      ['SHIP-141', 'SHIP-90'],
      ['SHIP-141', 'SHIP-102'],
    ]) {
      const drop = treeTarget(rows, row(dragged), row(group))!;
      expect(drop.placement).toEqual({ under: row(group).id });
      expect(drop.depth).toBe(row(group).depth + 1);
    }
  });
});

describe('placeRow', () => {
  const view = treeView(rows);
  const order = (v: ReturnType<typeof treeView>) => v.rows.map((r) => r.key);

  it('puts the row right after a group that has no stories of its own, at the new indent', () => {
    const drop = treeTarget(rows, row('SHIP-129'), row('SHIP-110'))!;
    const placed = placeRow(view, row('SHIP-129').id, drop);
    const at = order(placed).indexOf('SHIP-110');
    expect(order(placed).slice(at, at + 3)).toEqual(['SHIP-110', 'SHIP-129', 'SHIP-150']);
    expect(placed.rows.find((r) => r.key === 'SHIP-129')!.depth).toBe(3);
  });

  it('puts the row just above the story it was dropped on', () => {
    const drop = treeTarget(rows, row('SHIP-141'), row('SHIP-129'))!;
    const placed = placeRow(view, row('SHIP-141').id, drop);
    expect(order(placed).slice(order(placed).indexOf('SHIP-102'))).toEqual([
      'SHIP-102',
      'SHIP-141',
      'SHIP-129',
      '',
      'SHIP-170',
    ]);
  });
});

describe('withWritten', () => {
  it('puts the answered revision and the new parent into one row and leaves the rest', () => {
    const next = withWritten(treeView(rows), row('SHIP-141').id, 5, null);
    const moved = next.rows.find((r) => r.key === 'SHIP-141')!;
    expect(moved.revision).toBe(5);
    expect(moved.parent).toBeNull();
    expect(next.rows.find((r) => r.key === 'SHIP-144')!.revision).toBe(1);
    expect(withWritten(treeView(rows), row('SHIP-141').id, 5, undefined).rows[3].parent).toBe(
      row('SHIP-141').parent,
    );
  });
});
