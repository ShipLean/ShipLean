import { expect, test } from 'vitest';
import { matchRoute } from '../../routes';
import { opensInModal } from './opensInModal';

const at = (path: string) => {
  const here = matchRoute(path, '');
  const openKey = here !== undefined && 'key' in here.params ? here.params.key : undefined;
  return { here, openKey };
};

test('opensInModal_WhenSameProjectOnABoard_ShouldBeTrue', () => {
  const { here, openKey } = at('/p/SHIP/boards/stories');
  expect(opensInModal(here, openKey, 'SHIP')).toBe(true);
});

test('opensInModal_WhenOnABacklogsPage_ShouldBeFalse', () => {
  const { here, openKey } = at('/p/SHIP/backlogs/stories');
  expect(here?.id).toBe('storiesBacklog');
  expect(opensInModal(here, openKey, 'SHIP')).toBe(false);
});

test('opensInModal_WhenAnotherProject_ShouldBeFalse', () => {
  const { here, openKey } = at('/p/SHIP/boards/stories');
  expect(opensInModal(here, openKey, 'OTHER')).toBe(false);
});

test('opensInModal_WhenInstallLevelPage_ShouldBeFalse', () => {
  const { here, openKey } = at('/profile');
  expect(opensInModal(here, openKey, 'SHIP')).toBe(false);
});
