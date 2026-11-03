import { expect, test } from 'vitest';
import { matchRoute } from '../../routes';
import { opensInModal } from './opensInModal';

const at = (address: string) => {
  const [path = '', search = ''] = address.split('?');
  const here = matchRoute(path, search === '' ? '' : `?${search}`);
  const openKey = here !== undefined && 'key' in here.params ? here.params.key : undefined;
  return { here, openKey };
};

test('opensInModal_WhenSameProjectOnABoard_ShouldBeTrue', () => {
  const { here, openKey } = at('/p/SHIP/boards/stories');
  expect(opensInModal(here, openKey, 'SHIP')).toBe(true);
});

test('opens_on_each_backlog', () => {
  for (const path of [
    '/p/SHIP/backlogs/stories',
    '/p/SHIP/backlogs/stories?mode=planning',
    '/p/SHIP/backlogs/features',
    '/p/SHIP/backlogs/epics',
  ]) {
    const { here, openKey } = at(path);
    expect(opensInModal(here, openKey, 'SHIP'), path).toBe(true);
  }
});

test('not_for_another_project', () => {
  const { here, openKey } = at('/p/SHIP/backlogs/stories');
  expect(opensInModal(here, openKey, 'OTHER')).toBe(false);
});

test('opensInModal_WhenAnotherProject_ShouldBeFalse', () => {
  const { here, openKey } = at('/p/SHIP/boards/stories');
  expect(opensInModal(here, openKey, 'OTHER')).toBe(false);
});

test('opensInModal_WhenInstallLevelPage_ShouldBeFalse', () => {
  const { here, openKey } = at('/profile');
  expect(opensInModal(here, openKey, 'SHIP')).toBe(false);
});
