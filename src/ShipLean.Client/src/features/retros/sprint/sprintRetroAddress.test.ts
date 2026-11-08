import { expect, test } from 'vitest';
import { parseSprintRetroAddress, withSprintRetroValue } from './sprintRetroAddress';

test('sprintRetroAddress_WhenDecideAndPointAreGiven_ShouldReadThem', () => {
  expect(parseSprintRetroAddress(new URLSearchParams('decide=open&point=p1'))).toEqual({
    decide: true,
    point: 'p1',
    view: 'category',
    writtenBy: 'all',
    cat: 'wentWell',
  });
});

test('sprintRetroAddress_WhenEmpty_ShouldDefaultToAllPointsByCategoryAndNoPoint', () => {
  expect(parseSprintRetroAddress(new URLSearchParams(''))).toEqual({
    decide: false,
    point: undefined,
    view: 'category',
    writtenBy: 'all',
    cat: 'wentWell',
  });
});

test('sprintRetroAddress_WhenViewAndWrittenByAreGiven_ShouldReadThemAndIgnoreJunk', () => {
  const read = parseSprintRetroAddress(new URLSearchParams('view=item&writtenBy=agents&decide=x'));
  expect(read).toMatchObject({ view: 'item', writtenBy: 'agents', decide: false });
  expect(parseSprintRetroAddress(new URLSearchParams('view=zzz&writtenBy=zzz'))).toMatchObject({
    view: 'category',
    writtenBy: 'all',
    cat: 'wentWell',
  });
});

test('withSprintRetroValue_ShouldSetANonDefaultAndDropADefault', () => {
  const set = withSprintRetroValue(new URLSearchParams('point=p1'), 'view', 'item');
  expect(set.toString()).toBe('point=p1&view=item');
  expect(withSprintRetroValue(set, 'view', 'category').toString()).toBe('point=p1');
  expect(withSprintRetroValue(set, 'writtenBy', 'people').toString()).toBe(
    'point=p1&view=item&writtenBy=people',
  );
});

test('sprintRetroAddress_WhenCatIsGiven_ShouldReadItAndIgnoreJunk', () => {
  expect(parseSprintRetroAddress(new URLSearchParams('cat=notInPlan')).cat).toBe('notInPlan');
  expect(parseSprintRetroAddress(new URLSearchParams('cat=zzz')).cat).toBe('wentWell');
});

test('withSprintRetroValue_WhenCat_ShouldSetItAndDropWentWell', () => {
  const set = withSprintRetroValue(new URLSearchParams('decide=open'), 'cat', 'wentBadly');
  expect(set.toString()).toBe('decide=open&cat=wentBadly');
  expect(withSprintRetroValue(set, 'cat', 'wentWell').toString()).toBe('decide=open');
});
