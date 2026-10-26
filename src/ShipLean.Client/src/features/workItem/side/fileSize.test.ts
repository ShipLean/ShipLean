import { expect, test } from 'vitest';
import { formatSize } from './fileSize';

test.each([
  [0, '0 B'],
  [1023, '1023 B'],
  [1024, '1 KB'],
  [421888, '412 KB'],
  [1258291, '1.2 MB'],
])('formatSize(%d) is %s', (bytes, label) => {
  expect(formatSize(bytes)).toBe(label);
});
