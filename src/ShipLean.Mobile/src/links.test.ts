import { expect, test } from 'vitest';
import { links } from './links';

test('links_WhenBuilt_ShouldGiveTheRoutesObjectForm', () => {
  expect(links.signIn()).toEqual({ pathname: '/sign-in', params: {} });
  expect(links.projects()).toEqual({ pathname: '/', params: {} });
  expect(links.account()).toEqual({ pathname: '/account', params: {} });
});
