import { expect, test } from 'vitest';
import { matchRoute } from '../../routes';
import { sameScreenIn } from './sameScreenIn';

test('sameScreenIn_WhenSprintRoute_ShouldFallBackToAllSprints', () => {
  for (const path of [
    '/p/SHIP/sprints/abc/backlog',
    '/p/SHIP/sprints/abc/taskboard',
    '/p/SHIP/sprints/abc/capacity',
  ]) {
    expect(sameScreenIn(matchRoute(path), 'PAY')).toBe('/p/PAY/sprints');
  }
});

test('sameScreenIn_WhenWorkItemRoute_ShouldFallBackToTheStoriesBacklog', () => {
  expect(sameScreenIn(matchRoute('/p/SHIP/items/SHIP-141'), 'PAY')).toBe('/p/PAY/backlogs/stories');
  expect(sameScreenIn(matchRoute('/p/SHIP/items/SHIP-101/attachments/a1'), 'PAY')).toBe(
    '/p/PAY/backlogs/stories',
  );
  expect(sameScreenIn(matchRoute('/p/SHIP/items/SHIP-101/board', '?settings=card'), 'PAY')).toBe(
    '/p/PAY/backlogs/stories',
  );
});

test('sameScreenIn_WhenProjectScreen_ShouldKeepTheScreenAndItsQuery', () => {
  expect(sameScreenIn(matchRoute('/p/SHIP/backlogs/features', '?mode=tree'), 'PAY')).toBe(
    '/p/PAY/backlogs/features?mode=tree',
  );
  expect(sameScreenIn(matchRoute('/p/SHIP/settings/members'), 'PAY')).toBe(
    '/p/PAY/settings/members',
  );
  expect(sameScreenIn(matchRoute('/p/SHIP/boards/epics', '?settings=columns'), 'PAY')).toBe(
    '/p/PAY/boards/epics?settings=columns',
  );
});

test('sameScreenIn_WhenInstallLevelOrUnknown_ShouldOpenTheStoriesBacklog', () => {
  expect(sameScreenIn(matchRoute('/profile'), 'PAY')).toBe('/p/PAY/backlogs/stories');
  expect(sameScreenIn(undefined, 'PAY')).toBe('/p/PAY/backlogs/stories');
});

test('sameScreenIn_WhenAnItemIsOpenInAModal_ShouldDropTheItem', () => {
  expect(sameScreenIn(matchRoute('/p/SHIP/boards/stories', '?item=SHIP-12'), 'PAY')).toBe(
    '/p/PAY/boards/stories',
  );
});
