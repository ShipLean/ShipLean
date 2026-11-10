import { afterEach, describe, expect, it, vi } from 'vitest';
import { retroKeys, supportPoint, unsupportPoint } from './retrosApi';
import { noContent, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('retrosApi', () => {
  it('supportPoint POSTs and unsupportPoint DELETEs the same route with no body', async () => {
    const post = stubFetch(noContent());
    await expect(supportPoint('p/1')).resolves.toBeUndefined();
    const del = stubFetch(noContent());
    await expect(unsupportPoint('p/1')).resolves.toBeUndefined();
    expect(sent(post)).toEqual({
      url: '/api/retros/points/p%2F1/support',
      method: 'POST',
      body: undefined,
    });
    expect(sent(del)).toEqual({
      url: '/api/retros/points/p%2F1/support',
      method: 'DELETE',
      body: undefined,
    });
  });

  it('retroKeys differ by id, range and query', () => {
    expect(retroKeys.sprint('s')).toEqual(['retro', 'sprint', 's']);
    expect(retroKeys.insights('p', 'all')).not.toEqual(retroKeys.insights('p', '6'));
    expect(retroKeys.features('p')).toEqual(['projects', 'p', 'retros', 'features']);
    expect(retroKeys.actionDialog('p', { itemId: 'a' })).not.toEqual(
      retroKeys.actionDialog('p', { itemId: 'b' }),
    );
  });
});
