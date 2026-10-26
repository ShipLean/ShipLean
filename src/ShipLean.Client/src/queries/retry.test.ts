import { expect, test } from 'vitest';
import { ApiRequestError } from '../api/client';
import { retryUnlessClientError } from './retry';

test('retryUnlessClientError_WhenClientError_ShouldNotRetry', () => {
  expect(retryUnlessClientError(0, new ApiRequestError('Not found.', 404, 'not_found'))).toBe(
    false,
  );
  expect(retryUnlessClientError(0, new ApiRequestError('No.', 403, 'forbidden'))).toBe(false);
  expect(retryUnlessClientError(1, new ApiRequestError('Bad.', 400, 'validation'))).toBe(false);
});

test('retryUnlessClientError_WhenServerError_ShouldRetryThreeTimes', () => {
  const error = new ApiRequestError('Boom.', 500, 'internal_error');
  expect([0, 1, 2, 3].map((count) => retryUnlessClientError(count, error))).toEqual([
    true,
    true,
    true,
    false,
  ]);
});

test('retryUnlessClientError_WhenNetworkError_ShouldRetryThreeTimes', () => {
  const error = new TypeError('Failed to fetch');
  expect([0, 2, 3].map((count) => retryUnlessClientError(count, error))).toEqual([
    true,
    true,
    false,
  ]);
});
