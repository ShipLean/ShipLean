import type { ThemeTypes } from '@pierre/diffs';

// @pierre/diffs draws inside a shadow root, so Tailwind classes never reach it. Custom properties do inherit through the
// shadow boundary, so this maps its --diffs-* variables onto ShipLean's tokens (light and dark follow the tokens).
export const diffUnsafeCss = `
:host {
  --diffs-bg: var(--color-surface);
  --diffs-fg: var(--color-text);
  --diffs-fg-number: var(--color-muted);
  --diffs-addition-base: var(--color-ok);
  --diffs-deletion-base: var(--color-danger);
  --diffs-modified-base: var(--color-info);
  --diffs-font-family: var(--font-mono);
  --diffs-font-size: var(--text-xs);
  --diffs-line-height: 1.6;
}
[data-separator] { background: color-mix(in srgb, var(--color-info) 9%, var(--color-surface)); color: var(--color-muted); }
[data-utility-button] { background: var(--color-accent); color: var(--color-accent-fg); border-radius: var(--radius-xs); cursor: pointer; }
`;

export const diffThemes = { light: 'github-light', dark: 'github-dark' } as const;

/** The appearance the page is in now: an explicit data-theme wins over the system setting. */
export function currentThemeType(): ThemeTypes {
  const explicit = document.documentElement.dataset.theme;
  if (explicit === 'light' || explicit === 'dark') return explicit;
  return 'system';
}
