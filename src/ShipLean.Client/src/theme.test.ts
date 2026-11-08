import { afterEach, expect, test } from 'vitest';
import {
  appearanceStorageKey,
  applyAppearance,
  initAppearance,
  setDensity,
  setTheme,
} from './theme';

afterEach(() => {
  delete document.documentElement.dataset.theme;
  delete document.documentElement.dataset.density;
  localStorage.clear();
});

test('theme_WhenAppearanceApplied_ShouldBeRestoredOnTheNextLoad', () => {
  applyAppearance('light', 'comfortable');
  delete document.documentElement.dataset.theme;
  delete document.documentElement.dataset.density;

  initAppearance();
  expect(document.documentElement.dataset.theme).toBe('light');
  expect(document.documentElement.dataset.density).toBe('comfortable');
});

test('theme_WhenRememberedThemeIsOs_ShouldFollowTheOs', () => {
  applyAppearance('os', 'compact');
  document.documentElement.dataset.theme = 'dark';

  initAppearance();
  expect(document.documentElement.dataset.theme).toBeUndefined();
  expect(document.documentElement.dataset.density).toBe('compact');
});

test('theme_WhenStoredAppearanceIsUnreadable_ShouldUseTheDefaults', () => {
  localStorage.setItem(appearanceStorageKey, '{not json');

  initAppearance();
  expect(document.documentElement.dataset.theme).toBeUndefined();
  expect(document.documentElement.dataset.density).toBe('compact');
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
