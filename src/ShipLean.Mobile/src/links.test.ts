import { describe, expect, it, test } from 'vitest';
import { links, webPageUrl, webUrl } from './links';

test('links_WhenBuilt_ShouldGiveTheRoutesObjectForm', () => {
  expect(links.signIn()).toEqual({ pathname: '/sign-in', params: {} });
  expect(links.projects()).toEqual({ pathname: '/', params: {} });
  expect(links.account()).toEqual({ pathname: '/account', params: {} });
});

describe('links', () => {
  it('builds every link and leaves out unset options', () => {
    expect(links.inbox('SHIP')).toStrictEqual({
      pathname: '/p/[key]/inbox',
      params: { key: 'SHIP' },
    });
    expect(links.inbox('SHIP', 'setup')).toStrictEqual({
      pathname: '/p/[key]/inbox',
      params: { key: 'SHIP', from: 'setup' },
    });
    expect(links.backlog('SHIP')).toStrictEqual({
      pathname: '/p/[key]/backlog',
      params: { key: 'SHIP' },
    });
    expect(links.backlog('SHIP', { level: 'stories' })).toStrictEqual({
      pathname: '/p/[key]/backlog',
      params: { key: 'SHIP', level: 'stories' },
    });
    expect(
      links.backlog('SHIP', { level: 'features', epic: 'SHIP-3', feature: 'SHIP-12' }),
    ).toStrictEqual({
      pathname: '/p/[key]/backlog',
      params: { key: 'SHIP', level: 'features', epic: 'SHIP-3', feature: 'SHIP-12' },
    });
    expect(Object.keys(links.backlog('SHIP', { feature: 'SHIP-12' }).params)).toStrictEqual([
      'key',
      'feature',
    ]);
    expect(links.features('SHIP')).toStrictEqual({
      pathname: '/p/[key]/features',
      params: { key: 'SHIP' },
    });
    expect(links.retro('SHIP')).toStrictEqual({
      pathname: '/p/[key]/retro',
      params: { key: 'SHIP' },
    });
    expect(links.item('SHIP', 'SHIP-141')).toStrictEqual({
      pathname: '/p/[key]/items/[itemKey]',
      params: { key: 'SHIP', itemKey: 'SHIP-141' },
    });
    expect(links.featureBoard('SHIP', 'SHIP-12')).toStrictEqual({
      pathname: '/p/[key]/features/[featureKey]/board',
      params: { key: 'SHIP', featureKey: 'SHIP-12' },
    });
    expect(links.digest('SHIP')).toStrictEqual({
      pathname: '/p/[key]/features/digest',
      params: { key: 'SHIP' },
    });
    expect(links.settingsVoice()).toStrictEqual({ pathname: '/settings/voice', params: {} });
    expect(links.licences()).toStrictEqual({ pathname: '/settings/voice/licences', params: {} });
    for (const step of ['models', 'microphone', 'downloading'] as const)
      expect(links.setup(step)).toStrictEqual({ pathname: '/setup/[step]', params: { step } });
  });

  it('webUrl builds the item and board pages from the origin', () => {
    expect(webUrl('https://shiplean.test', 'SHIP', 'SHIP-141')).toBe(
      'https://shiplean.test/p/SHIP/items/SHIP-141',
    );
    expect(webUrl('https://shiplean.test', 'SHIP', 'SHIP-141', 'item')).toBe(
      'https://shiplean.test/p/SHIP/items/SHIP-141',
    );
    expect(webUrl('https://shiplean.test', 'SHIP', 'SHIP-12', 'board')).toBe(
      'https://shiplean.test/p/SHIP/items/SHIP-12/board',
    );
  });
});

describe('webPageUrl', () => {
  it('gives the web Backlog at a level, Stories when none is given, and the Planning view on Stories', () => {
    const origin = 'https://shiplean.example';
    expect(webPageUrl(origin, 'SHIP', { page: 'backlog', level: 'features' })).toBe(
      'https://shiplean.example/p/SHIP/backlogs/features',
    );
    expect(webPageUrl(origin, 'SHIP', { page: 'backlog', level: 'epics' })).toBe(
      'https://shiplean.example/p/SHIP/backlogs/epics',
    );
    expect(webPageUrl(origin, 'SHIP', { page: 'backlog' })).toBe(
      'https://shiplean.example/p/SHIP/backlogs/stories',
    );
    expect(webPageUrl(origin, 'SHIP', { page: 'planning' })).toBe(
      'https://shiplean.example/p/SHIP/backlogs/stories?mode=planning',
    );
  });
});
