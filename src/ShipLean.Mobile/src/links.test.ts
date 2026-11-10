import { describe, expect, it, test } from 'vitest';
import { links, webUrl } from './links';

test('links_WhenBuilt_ShouldGiveTheRoutesObjectForm', () => {
  expect(links.signIn()).toEqual({ pathname: '/sign-in', params: {} });
  expect(links.projects()).toEqual({ pathname: '/', params: {} });
  expect(links.account()).toEqual({ pathname: '/account', params: {} });
});

describe('links', () => {
  it('builds every link and leaves out unset options', () => {
    expect(links.inbox('SHIP')).toEqual({ pathname: '/p/[key]/inbox', params: { key: 'SHIP' } });
    expect(links.inbox('SHIP', 'setup')).toEqual({
      pathname: '/p/[key]/inbox',
      params: { key: 'SHIP', from: 'setup' },
    });
    expect(links.backlog('SHIP')).toEqual({
      pathname: '/p/[key]/backlog',
      params: { key: 'SHIP' },
    });
    expect(links.backlog('SHIP', { level: 'stories' })).toEqual({
      pathname: '/p/[key]/backlog',
      params: { key: 'SHIP', level: 'stories' },
    });
    expect(
      links.backlog('SHIP', { level: 'features', epic: 'SHIP-3', feature: 'SHIP-12' }),
    ).toEqual({
      pathname: '/p/[key]/backlog',
      params: { key: 'SHIP', level: 'features', epic: 'SHIP-3', feature: 'SHIP-12' },
    });
    expect(Object.keys(links.backlog('SHIP', { feature: 'SHIP-12' }).params)).toEqual([
      'key',
      'feature',
    ]);
    expect(links.features('SHIP')).toEqual({
      pathname: '/p/[key]/features',
      params: { key: 'SHIP' },
    });
    expect(links.retro('SHIP')).toEqual({ pathname: '/p/[key]/retro', params: { key: 'SHIP' } });
    expect(links.item('SHIP', 'SHIP-141')).toEqual({
      pathname: '/p/[key]/items/[itemKey]',
      params: { key: 'SHIP', itemKey: 'SHIP-141' },
    });
    expect(links.featureBoard('SHIP', 'SHIP-12')).toEqual({
      pathname: '/p/[key]/features/[featureKey]/board',
      params: { key: 'SHIP', featureKey: 'SHIP-12' },
    });
    expect(links.digest('SHIP')).toEqual({
      pathname: '/p/[key]/features/digest',
      params: { key: 'SHIP' },
    });
    expect(links.settingsVoice()).toEqual({ pathname: '/settings/voice', params: {} });
    expect(links.licences()).toEqual({ pathname: '/settings/voice/licences', params: {} });
    for (const step of ['models', 'microphone', 'downloading'] as const)
      expect(links.setup(step)).toEqual({ pathname: '/setup/[step]', params: { step } });
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
