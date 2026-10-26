import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { story } from '../ct/fixtures';
import { workItemKey } from '../workItemApi';
import { cacheIfNewer } from './cacheIfNewer';

const key = workItemKey(story.projectId, story.key);
const at = (revision: number) => ({ ...story, revision });

describe('cacheIfNewer', () => {
  it('cacheIfNewer_WhenNothingIsCached_ShouldStoreIt', () => {
    const client = new QueryClient();
    cacheIfNewer(client, at(8));
    expect(client.getQueryData(key)).toEqual(at(8));
  });

  it('cacheIfNewer_WhenTheCacheIsOlder_ShouldReplaceIt', () => {
    const client = new QueryClient();
    client.setQueryData(key, at(7));
    cacheIfNewer(client, at(8));
    expect(client.getQueryData(key)).toEqual(at(8));
  });

  it('cacheIfNewer_WhenTheCacheIsNewer_ShouldKeepIt', () => {
    const client = new QueryClient();
    client.setQueryData(key, at(9));
    cacheIfNewer(client, at(8));
    expect(client.getQueryData(key)).toEqual(at(9));
  });
});
