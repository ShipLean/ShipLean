import { expect, test } from 'vitest';
import { shortSprintName } from './shortSprintName';

test('shortSprintName_WhenNameIsYearQuarterSprint_ShouldKeepYearQuarterAndNumber', () => {
  expect(shortSprintName('2026 Q4 Sprint 3')).toBe('2026 Q4 S3');
  expect(shortSprintName('2026 Q4 Sprint 12')).toBe('2026 Q4 S12');
});

test('shortSprintName_WhenNameIsAnyOther_ShouldKeepItWhole', () => {
  expect(shortSprintName('Sprint 24')).toBe('Sprint 24');
});
