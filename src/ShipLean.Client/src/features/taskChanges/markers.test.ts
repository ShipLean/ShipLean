import { expect, test } from 'vitest';
import type { CodeCommentDto } from '@shiplean/core/api/taskChanges';
import { deriveMarkers } from './markers';

const comment = (over: Partial<CodeCommentDto>): CodeCommentDto => ({
  id: 'c',
  parentId: null,
  repo: 'acme/web',
  sha: 'abc',
  path: 'a.ts',
  side: 'new',
  line: 3,
  startLine: null,
  lineText: 'x',
  bodyHtml: '<p>x</p>',
  by: 'Priya',
  byAgent: false,
  createdAtLabel: 'Today',
  resolved: false,
  outdated: false,
  sentBack: false,
  replies: [],
  ...over,
});

test('deriveMarkers_WhenThreadsShareALine_ShouldMakeOneMarkerWithCounts', () => {
  const markers = deriveMarkers([
    comment({ id: '1', replies: [comment({ id: 'r', parentId: '1' })] }),
    comment({ id: '2', resolved: true }),
    comment({ id: '3', line: 9 }),
    comment({ id: '4', repo: 'acme/api' }),
  ]);

  expect(markers).toHaveLength(3);
  expect(markers[0]).toMatchObject({
    line: 3,
    repo: 'acme/web',
    count: 3,
    open: 1,
    outdated: false,
  });
});

test('deriveMarkers_WhenAThreadIsOutdated_ShouldFlagItsMarker', () => {
  const [marker] = deriveMarkers([comment({ outdated: true })]);

  expect(marker?.outdated).toBe(true);
});
