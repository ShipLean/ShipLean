import { expect, test } from 'vitest';
import {
  meKey,
  preferenceKey,
  projectBoardsKey,
  projectByKeyKey,
  projectMembersKey,
  projectsKey,
  projectSprintsKey,
} from './shared';

test('sharedKeys_ShouldBeTheDocumentedArrays', () => {
  expect(meKey).toEqual(['me']);
  expect(projectsKey).toEqual(['projects']);
  expect(projectByKeyKey('SHIP')).toEqual(['projects', 'by-key', 'SHIP']);
  expect(projectSprintsKey('p1')).toEqual(['projects', 'p1', 'sprints']);
  expect(projectMembersKey('p1')).toEqual(['projects', 'p1', 'members']);
  expect(projectBoardsKey('p1')).toEqual(['projects', 'p1', 'boards']);
  expect(preferenceKey('recentProjects')).toEqual(['preferences', 'recentProjects']);
});
