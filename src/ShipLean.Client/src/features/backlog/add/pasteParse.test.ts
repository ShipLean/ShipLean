import { expect, test } from 'vitest';
import { lineCount, parsePaste, pointsError, pointsValue } from './pasteParse';

const shape = (text: string) =>
  parsePaste(text).map((row) => [row.kind, row.type, row.title, row.points]);

test('indented_line_is_a_task', () => {
  expect(shape('Checkout\n  Confirm dialog\n\tReceipt')).toEqual([
    ['item', 'story', 'Checkout', ''],
    ['task', 'task', 'Confirm dialog', ''],
    ['task', 'task', 'Receipt', ''],
  ]);
});

test('bug_prefix_makes_a_bug', () => {
  const rows = parsePaste('bug: Session expires\n  bug: not a bug task');
  expect(rows[0]).toMatchObject({ type: 'bug', title: 'Session expires' });
  expect(rows[1]).toMatchObject({ kind: 'task', type: 'task', title: 'bug: not a bug task' });
});

test('default_type_applies_to_items_without_a_prefix', () => {
  const rows = parsePaste('One\nbug: Two', 'bug');
  expect(rows.map((row) => row.type)).toEqual(['bug', 'bug']);
});

test('trailing_number_column_sets_points', () => {
  expect(shape('Apple Pay\t5\nGift card  8')).toEqual([
    ['item', 'story', 'Apple Pay', '5'],
    ['item', 'story', 'Gift card', '8'],
  ]);
  expect(parsePaste('Bad\tlots')[0].points).toBe('lots');
  expect(pointsError('lots')).not.toBeNull();
  expect(pointsError('2.5')).toBeNull();
  expect(pointsValue('2.5')).toBe(2.5);
  expect(pointsValue('')).toBeNull();
});

test('pts_in_brackets_sets_points', () => {
  expect(shape('Gift card (3 pts)\nOther (1pt)')).toEqual([
    ['item', 'story', 'Gift card', '3'],
    ['item', 'story', 'Other', '1'],
  ]);
});

test('excel_columns_with_trailing_tab', () => {
  expect(shape('One\t3\t\r\nTwo\t\t\r\n')).toEqual([
    ['item', 'story', 'One', '3'],
    ['item', 'story', 'Two', ''],
  ]);
});

test('bullets_are_stripped', () => {
  expect(shape('- One\n* Two\n  - Sub\n-Three')).toEqual([
    ['item', 'story', 'One', ''],
    ['item', 'story', 'Two', ''],
    ['task', 'task', 'Sub', ''],
    ['item', 'story', '-Three', ''],
  ]);
});

test('task_with_no_story_becomes_a_story', () => {
  const rows = parsePaste('  Lonely\n  Child\nNext');
  expect(rows.map((row) => [row.kind, row.promoted])).toEqual([
    ['item', true],
    ['task', false],
    ['item', false],
  ]);
});

test('order_is_kept', () => {
  const rows = parsePaste('C\n\n  c1\nA\nB\n   \nZ');
  expect(rows.map((row) => row.title)).toEqual(['C', 'c1', 'A', 'B', 'Z']);
  expect(rows.map((row) => row.id)).toEqual([0, 1, 2, 3, 4]);
  expect(lineCount('C\n\n  c1\nA\nB\n   \nZ')).toBe(5);
});
