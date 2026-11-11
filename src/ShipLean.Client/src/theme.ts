import type { Density, Theme as SavedTheme } from '@shiplean/core/api/me';

export type Theme = 'light' | 'dark';

// Both live on <html> and are read by styles/tokens.css. No theme attribute means the page follows the OS.
// Until the signed-in user's preferences arrive (the shell applies them), the page shows the appearance this browser
// last applied, or follows the OS and is compact on a first visit.
export const defaultTheme: Theme | undefined = undefined;
export const defaultDensity: Density = 'compact';

// The last appearance applied in this browser. index.html reads the same key in an inline script, before first paint,
// so a saved theme that differs from the OS does not flash the OS theme while the app loads (SHIP-745).
export const appearanceStorageKey = 'shiplean.appearance';

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
  try {
    localStorage.setItem(appearanceStorageKey, JSON.stringify({ theme, density }));
  } catch {
    // Storage blocked (private window, site data off): the next load follows the OS until the profile arrives.
  }
}

function rememberedAppearance(): { theme: Theme | undefined; density: Density } | undefined {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(appearanceStorageKey) ?? 'null');
    if (typeof parsed !== 'object' || parsed === null) return undefined;
    const { theme, density } = parsed as Record<string, unknown>;
    return {
      theme: theme === 'light' || theme === 'dark' ? theme : undefined,
      density: density === 'comfortable' ? 'comfortable' : 'compact',
    };
  } catch {
    return undefined;
  }
}

// Called once at startup: keeps what index.html set from storage, or the defaults on a first visit.
export function initAppearance() {
  const remembered = rememberedAppearance();
  setTheme(remembered ? remembered.theme : defaultTheme);
  setDensity(remembered ? remembered.density : defaultDensity);
}
