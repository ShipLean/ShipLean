import type { Density, Theme as SavedTheme } from './api/me';

export type Theme = 'light' | 'dark';

// Both live on <html> and are read by styles/tokens.css. No theme attribute means the page follows the OS.
// Until the signed-in user's preferences arrive (the shell applies them), the page follows the OS and is compact.
export const defaultTheme: Theme | undefined = undefined;
export const defaultDensity: Density = 'compact';

export function setTheme(theme: Theme | undefined) {
  if (theme === undefined) delete document.documentElement.dataset.theme;
  else document.documentElement.dataset.theme = theme;
}

export function setDensity(density: Density) {
  document.documentElement.dataset.density = density;
}

// A saved theme ('os' follows the OS: no attribute) and density, as the user's profile holds them.
export function applyAppearance(theme: SavedTheme, density: Density) {
  setTheme(theme === 'os' ? undefined : theme);
  setDensity(density);
}

// Called once at startup.
export function initAppearance() {
  setTheme(defaultTheme);
  setDensity(defaultDensity);
}
