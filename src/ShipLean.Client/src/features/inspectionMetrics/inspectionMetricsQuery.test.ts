import { expect, test } from 'vitest';
import {
  apiQueryOf,
  DEFAULT_INSPECTION_QUERY,
  queryFromSearch,
  queryToSearch,
  type InspectionQuery,
} from './inspectionMetricsQuery';

const full: InspectionQuery = {
  view: 'period',
  range: 'lastQuarter',
  cut: 'who',
  period: '00000004-0000-4000-8001-000000000023',
  select: 'SHIP-194',
  tab: 'features',
};

test('queryFromSearch_WhenEveryParamSet_ShouldRoundTrip', () => {
  const search = queryToSearch(full).toString();
  expect(search).toBe(
    'view=period&range=lastQuarter&cut=who&p=00000004-0000-4000-8001-000000000023&select=SHIP-194&tab=features',
  );
  expect(queryFromSearch(new URLSearchParams(search))).toEqual(full);
});

test.each([
  ['view', 'leadtime'],
  ['view', 'json'],
  ['range', 'quarter'],
  ['range', 'all'],
  ['cut', 'feature'],
  ['p', 's-1'],
  ['select', 'SHIP-1'],
  ['tab', 'features'],
])('queryFromSearch_When_%s_Is_%s_ShouldRoundTripIt', (name, value) => {
  const search = `${name}=${value}`;
  expect(queryToSearch(queryFromSearch(new URLSearchParams(search))).toString()).toBe(search);
});

test('queryFromSearch_WhenEmpty_ShouldGiveTheDefaultsAndWriteNothing', () => {
  expect(queryFromSearch(new URLSearchParams())).toEqual(DEFAULT_INSPECTION_QUERY);
  expect(queryToSearch(DEFAULT_INSPECTION_QUERY).toString()).toBe('');
});

test('queryFromSearch_WhenValuesUnknown_ShouldFallBackToTheDefaults', () => {
  const query = queryFromSearch(new URLSearchParams('view=tokens&range=w6&cut=x&tab=y&p=&select='));
  expect(query).toEqual(DEFAULT_INSPECTION_QUERY);
});

test('apiQueryOf_WhenPeriodView_ShouldSendPeriodAndSelect', () => {
  expect(apiQueryOf(full)).toEqual({
    range: 'lastQuarter',
    cut: 'who',
    period: full.period,
    select: 'SHIP-194',
  });
});

test('apiQueryOf_WhenNotThePeriodView_ShouldSendNoPeriodOrSelect', () => {
  expect(apiQueryOf({ ...full, view: 'leadtime' })).toEqual({ range: 'lastQuarter', cut: 'who' });
});

test('apiQueryOf_WhenSelectWithoutPeriod_ShouldDropSelect', () => {
  expect(apiQueryOf({ ...full, period: null })).toEqual({ range: 'lastQuarter', cut: 'who' });
});
