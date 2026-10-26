import { afterEach, expect, test } from 'vitest';
import { applyAppearance, initAppearance, setDensity, setTheme } from './theme';

afterEach(() => {
  delete document.documentElement.dataset.theme;
  delete document.documentElement.dataset.density;
});

test('theme_WhenDarkSet_ShouldSetTheAttribute', () => {
  setTheme('dark');
  expect(document.documentElement.dataset.theme).toBe('dark');

  setTheme(undefined);
  expect(document.documentElement.dataset.theme).toBeUndefined();
});

test('theme_WhenAppearanceInitialised_ShouldFollowTheOsAndBeCompact', () => {
  initAppearance();
  expect(document.documentElement.dataset.theme).toBeUndefined();
  expect(document.documentElement.dataset.density).toBe('compact');

  setDensity('comfortable');
  expect(document.documentElement.dataset.density).toBe('comfortable');
});

test('theme_WhenOs_ShouldRemoveTheAttribute', () => {
  applyAppearance('dark', 'comfortable');
  expect(document.documentElement.dataset.theme).toBe('dark');
  expect(document.documentElement.dataset.density).toBe('comfortable');

  applyAppearance('os', 'compact');
  expect(document.documentElement.dataset.theme).toBeUndefined();
  expect(document.documentElement.dataset.density).toBe('compact');
});
