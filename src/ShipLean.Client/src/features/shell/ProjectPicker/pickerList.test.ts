import { expect, test } from 'vitest';
import type { ProjectSummaryDto } from '../../../api/projects';
import { pickerGroups } from './pickerList';

const project = (key: string, name: string): ProjectSummaryDto => ({
  id: `id-${key}`,
  key,
  name,
  role: 'member',
  memberCount: 3,
});
const all = [project('SHIP', 'Shipping'), project('PAY', 'Payments'), project('MOB', 'Mobile')];

test('pickerGroups_WhenNoSearch_ShouldListRecentThenTheRestAlphabetically', () => {
  const groups = pickerGroups(all, ['id-SHIP', 'id-gone'], '');
  expect(groups.map((g) => g.title)).toEqual(['Recent', 'All your projects']);
  expect(groups[0].projects.map((p) => p.key)).toEqual(['SHIP']);
  expect(groups[1].projects.map((p) => p.key)).toEqual(['MOB', 'PAY']);
});

test('pickerGroups_WhenSearchTyped_ShouldMatchKeyOrNameCaseInsensitively', () => {
  expect(pickerGroups(all, [], ' pay ')[0].projects.map((p) => p.key)).toEqual(['PAY']);
  expect(pickerGroups(all, [], 'mobi')[0].projects.map((p) => p.key)).toEqual(['MOB']);
  expect(pickerGroups(all, [], 'zzz')[0].projects).toEqual([]);
});
