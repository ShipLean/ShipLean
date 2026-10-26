import { afterEach, expect, test, vi } from 'vitest';
import { api } from '../../api/client';
import { fetchBacklog } from './backlogApi';

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: { get: vi.fn().mockResolvedValue({}) },
}));

afterEach(() => vi.mocked(api.get).mockClear());

test('fetchBacklog_WhenStories_ShouldReadTheStoriesEndpointWithItsMode', async () => {
  await fetchBacklog({
    projectId: 'p1',
    level: 'stories',
    mode: 'tree',
    filter: '',
    includeCompleted: false,
  });
  expect(api.get).toHaveBeenCalledWith(
    '/api/projects/p1/backlog?mode=tree&filter=&includeCompleted=false',
  );
});

test.each(['features', 'epics'] as const)(
  'fetchBacklog_When%sLevel_ShouldReadItsEndpointWithoutAMode',
  async (level) => {
    await fetchBacklog({
      projectId: 'p1',
      level,
      mode: 'list',
      filter: '{"a":1}',
      includeCompleted: true,
    });
    expect(api.get).toHaveBeenCalledWith(
      `/api/projects/p1/backlogs/${level}?filter=${encodeURIComponent('{"a":1}')}&includeCompleted=true`,
    );
  },
);
