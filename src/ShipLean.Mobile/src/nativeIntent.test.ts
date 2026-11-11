import { expect, test } from 'vitest';
import { redirectSystemPath } from '../app/+native-intent';

test('redirectSystemPath_WhenTheHandoffRedirectArrivesWhileRunning_ShouldLeaveTheRouterWhereItIs', () => {
  expect(
    redirectSystemPath({ path: 'shiplean://signed-in?code=x&state=y', initial: false }),
  ).toBeNull();
});

test('redirectSystemPath_WhenTheAppStartsFromTheHandoffRedirect_ShouldOpenSignIn', () => {
  expect(
    redirectSystemPath({ path: 'shiplean://signed-in?error=cancelled&state=y', initial: true }),
  ).toBe('/sign-in');
});

test('redirectSystemPath_WhenAnyOtherLinkArrives_ShouldPassItThrough', () => {
  expect(redirectSystemPath({ path: 'shiplean://signed-inx', initial: false })).toBe(
    'shiplean://signed-inx',
  );
  expect(redirectSystemPath({ path: '/account', initial: false })).toBe('/account');
});

test('redirectSystemPath_WhenTheRedirectArrivesAsARouterPath_ShouldTreatItTheSame', () => {
  expect(redirectSystemPath({ path: '/signed-in?code=x&state=y', initial: false })).toBeNull();
  expect(redirectSystemPath({ path: '/signed-in?code=x&state=y', initial: true })).toBe('/sign-in');
});
