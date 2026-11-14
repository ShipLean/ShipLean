import { afterEach, describe, expect, it, vi } from 'vitest';
import { postTimelineBarSelected, postTimelineOpened } from './usageApi';
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
