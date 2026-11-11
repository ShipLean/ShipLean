import type { Scheme } from '../theme/themes';

// The device colour scheme the jest mock of useColorScheme answers; renderScreen and renderApp set it.
let scheme: Scheme = 'light';

export function setScheme(next: Scheme): void {
  scheme = next;
}

export function currentScheme(): Scheme {
  return scheme;
}
