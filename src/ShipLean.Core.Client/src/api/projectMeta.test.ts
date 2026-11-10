import { afterEach, describe, expect, it, vi } from 'vitest';
import { getMembers, getSprints, getStates, projectMetaKeys } from './projectMeta';
import { ok, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('projectMeta', () => {
  it.each([
    ['getStates', getStates, 'states'],
    ['getMembers', getMembers, 'members'],
    ['getSprints', getSprints, 'sprints'],
  ] as const)('%s GETs the project %s route', async (_name, fetcher, segment) => {
    const stub = stubFetch(ok([]));
    await fetcher('p/1');
    expect(sent(stub)).toEqual({
      url: `/api/projects/p%2F1/${segment}`,
      method: 'GET',
      body: undefined,
    });
    expect(projectMetaKeys[segment]('p')).toEqual(['projects', 'p', segment]);
  });
});
