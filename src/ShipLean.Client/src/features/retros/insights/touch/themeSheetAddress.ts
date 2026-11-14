const THEME = 'theme:';

/** The theme the address asks the sheet to show (`sheet=theme:<themeId>`), or null. */
export function themeSheetId(params: URLSearchParams): string | null {
  const value = params.get('sheet');
  return value?.startsWith(THEME) ? value.slice(THEME.length) : null;
}

/** The address with the theme sheet open on a theme. */
export function withThemeSheet(params: URLSearchParams, themeId: string): URLSearchParams {
  const next = new URLSearchParams(params);
  next.set('sheet', `${THEME}${themeId}`);
  return next;
}

/** The address with the sheet closed. */
export function withoutSheet(params: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(params);
  next.delete('sheet');
  return next;
}
