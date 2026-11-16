import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  postBoardFilterOpened,
  postBoardHealthFiltered,
  postTimelineBarSelected,
  postTimelineOpened,
} from './usageApi';
import { noContent, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('timeline usage', () => {
  it('postTimelineOpened POSTs where it was opened from', async () => {
    const fetchStub = stubFetch(noContent());
    await postTimelineOpened('p/1', 'feature');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/timeline-opened',
      method: 'POST',
      body: { from: 'feature' },
    });
  });

  it('postTimelineBarSelected POSTs an empty body', async () => {
    const fetchStub = stubFetch(noContent());
    await postTimelineBarSelected('p/1');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/timeline-bar-selected',
      method: 'POST',
      body: {},
    });
  });
});

describe('board filter usage', () => {
  it('postBoardFilterOpened POSTs an empty body', async () => {
    const fetchStub = stubFetch(noContent());
    await postBoardFilterOpened('p/1');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/board-filter-opened',
      method: 'POST',
      body: {},
    });
  });

  it('postBoardHealthFiltered POSTs the chosen health', async () => {
    const fetchStub = stubFetch(noContent());
    await postBoardHealthFiltered('p/1', 'atRisk');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/board-health-filtered',
      method: 'POST',
      body: { health: 'atRisk' },
    });
  });
});
