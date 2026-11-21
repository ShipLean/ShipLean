import { afterEach, describe, expect, it, vi } from 'vitest';
import { promptCustomizeOpened, promptsOpened } from './usageApi';
import { noContent, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('prompts usage', () => {
  it('promptsOpened POSTs to prompts-opened', async () => {
    const fetchStub = stubFetch(noContent());
    await promptsOpened('p/1');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/prompts-opened',
      method: 'POST',
      body: {},
    });
  });

  it('promptCustomizeOpened POSTs the prompt', async () => {
    const fetchStub = stubFetch(noContent());
    await promptCustomizeOpened('p/1', 'planReviewer');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/prompt-customize-opened',
      method: 'POST',
      body: { prompt: 'planReviewer' },
    });
  });
});
